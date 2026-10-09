using UnityEngine;
using UnityEngine.UI;

// One HUD per viewport: position, lap, held item (with roulette), speed + drift charge, minimap,
// countdown / wrong way / finish text, hit messages and name tags over the other cars.
public class ViewHud
{
    public RectTransform panel;
    readonly RectTransform inner;
    readonly Text pos, posOf, lapT, speedT, center, toast, itemName, itemHint, driftT;
    readonly Image itemBg, itemIcon;
    readonly RawImage map;
    readonly Image[] dots = new Image[8];
    readonly Text[] tags = new Text[8];
    float scale = 1f;
    Rect vp;
    public float bottomInset;

    public ViewHud(Transform canvas, string name, Texture2D mapTex)
    {
        panel = UIK.Rect(canvas, "View " + name, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIK.Stretch(panel);
        inner = UIK.Rect(panel, "Inner", Vector2.zero, Vector2.zero, new Vector2(1280, 720));
        inner.pivot = Vector2.zero;
        inner.anchoredPosition = Vector2.zero;
        Transform r = inner;
        pos = UIK.Label(r, "", 64, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(120f, -50f), new Vector2(200f, 80f), Color.white);
        posOf = UIK.Label(r, "", 26, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(230f, -66f), new Vector2(120f, 40f), new Color(1f, 1f, 1f, 0.85f));
        lapT = UIK.Label(r, "", 28, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(140f, -112f), new Vector2(240f, 40f), new Color(1f, 0.95f, 0.6f));
        speedT = UIK.Label(r, "", 30, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(140f, 34f), new Vector2(240f, 44f), Color.white);
        driftT = UIK.Label(r, "", 24, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(140f, 74f), new Vector2(240f, 36f), Color.white);
        center = UIK.Label(r, "", 90, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1000f, 200f), Color.white);
        toast = UIK.Label(r, "", 30, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(900f, 80f), new Color(1f, 0.92f, 0.5f));
        itemBg = UIK.Img(r, null, new Color(0f, 0f, 0f, 0.45f), new Vector2(1f, 1f), new Vector2(-80f, -80f), new Vector2(116f, 116f));
        itemIcon = UIK.Img(itemBg.transform, UIK.Circle, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(70f, 70f));
        itemName = UIK.Label(itemBg.transform, "", 17, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(150f, 26f), Color.white);
        itemHint = UIK.Label(r, "", 15, TextAnchor.UpperCenter, new Vector2(1f, 1f), new Vector2(-80f, -152f), new Vector2(200f, 24f), new Color(1f, 1f, 1f, 0.75f));
        var mrt = UIK.Rect(r, "Map", new Vector2(1f, 1f), new Vector2(-92f, -262f), new Vector2(160f, 160f));
        UIK.Img(r, UIK.Circle, new Color(0f, 0.15f, 0.25f, 0.35f), new Vector2(1f, 1f), new Vector2(-92f, -262f), new Vector2(176f, 176f)).transform.SetSiblingIndex(mrt.GetSiblingIndex());
        map = mrt.gameObject.AddComponent<RawImage>();
        map.texture = mapTex;
        map.raycastTarget = false;
        for (int i = 0; i < 8; i++)
        {
            dots[i] = UIK.Img(mrt, UIK.Circle, Color.white, new Vector2(0f, 0f), Vector2.zero, new Vector2(10f, 10f));
            tags[i] = UIK.Label(r, "", 18, TextAnchor.MiddleCenter, new Vector2(0f, 0f), Vector2.zero, new Vector2(240f, 28f), Color.white);
        }
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
        if (p == 1) return "1st";
        if (p == 2) return "2nd";
        if (p == 3) return "3rd";
        return p + "th";
    }

    public void SetCenter(string s, Color c) { center.text = s; center.color = c; }

    public void Tick(Camera cam, Kart me, Game g, string centerOverride)
    {
        // fit the 1280x720 layout into this viewport
        Rect pr = panel.rect;
        if (pr.width > 1f)
        {
            inner.localScale = Vector3.one * scale;
            inner.sizeDelta = new Vector2(pr.width / scale, pr.height / scale);
        }
        int n = g.karts.Count;
        pos.text = Ord(me.place);
        pos.color = me.place == 1 ? new Color(1f, 0.85f, 0.2f) : Color.white;
        posOf.text = "/ " + n;
        int lapShow = Mathf.Clamp(me.lap + 1, 1, Track.Laps);
        lapT.text = me.finished ? "FINISHED" : "LAP " + lapShow + " / " + Track.Laps;
        speedT.text = Mathf.RoundToInt(Mathf.Abs(me.speed) * 3.6f) + " km/h";
        if (me.drifting) { driftT.text = me.driftCharge >= 2.3f ? "DRIFT  <color=#ff9a1a>MAX</color>" : me.driftCharge >= 1.1f ? "DRIFT  <color=#5aa8ff>BOOST</color>" : "DRIFT"; }
        else driftT.text = me.boostT > 0f ? "<color=#5dff7a>BOOST!</color>" : (me.shieldT > 0f ? "<color=#6fe0ff>SHIELD</color>" : "");
        // item slot
        Item show = me.rollT > 0f ? me.rollShow : me.item;
        if (show == Item.None) { itemIcon.enabled = false; itemName.text = ""; itemHint.text = ""; }
        else
        {
            itemIcon.enabled = true;
            itemIcon.color = Items.Col(show);
            itemName.text = Items.Name(show);
            itemHint.text = me.rollT > 0f ? "" : g.FireHint(me);
        }
        itemBg.color = me.rollT > 0f ? new Color(0.2f, 0.2f, 0.1f, 0.6f) : new Color(0f, 0f, 0f, 0.45f);
        // centre text
        if (!string.IsNullOrEmpty(centerOverride)) { center.text = centerOverride; center.color = Color.white; }
        else if (me.wrongT > 1.2f) { center.text = "<size=70>WRONG WAY!</size>"; center.color = new Color(1f, 0.35f, 0.3f); }
        else if (me.finished) { center.text = "<size=60>" + Ord(me.place) + " PLACE!</size>"; center.color = me.place <= 3 ? new Color(1f, 0.85f, 0.2f) : Color.white; }
        else center.text = "";
        toast.text = me.toastT > 0f ? me.toast : "";
        toast.rectTransform.anchoredPosition = new Vector2(0f, 120f + bottomInset / Mathf.Max(0.3f, scale));
        speedT.rectTransform.anchoredPosition = new Vector2(140f, 34f + bottomInset / Mathf.Max(0.3f, scale));
        driftT.rectTransform.anchoredPosition = new Vector2(140f, 74f + bottomInset / Mathf.Max(0.3f, scale));

        // minimap dots (leader drawn last = on top)
        RectTransform mrt = map.rectTransform;
        for (int i = 0; i < dots.Length; i++)
        {
            if (i >= n) { dots[i].enabled = false; continue; }
            Kart k = g.karts[i];
            Vector2 uv = Track.MapUV(k.transform.position);
            dots[i].enabled = true;
            dots[i].color = k.Color;
            dots[i].rectTransform.anchoredPosition = new Vector2(uv.x * mrt.sizeDelta.x, uv.y * mrt.sizeDelta.y);
            float sz = k == me ? 18f : k.human ? 14f : 10f;
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
            t.text = (k.human ? "P" + (k.slot + 1) + " " : "") + Robots.Names[k.robot];
            t.color = k.Color == Cars.All[7].color ? new Color(0.75f, 0.75f, 0.8f) : k.Color;
            t.fontSize = d > 45f ? 15 : 19;
            t.rectTransform.anchoredPosition = lp;
        }
    }
}
