using UnityEngine;
using UnityEngine.UI;

// Per-viewport HUD: a Screen Space - Camera canvas bound to the player's camera, so it fits that split-screen rect.
public class Hud
{
    public Canvas canvas;
    readonly Image[] balloonIcons = new Image[3];
    readonly Image[] crossBars = new Image[4];
    readonly Image dot;
    readonly Text ammo, top, center, pop, label, feed, hit;
    float popT, hitT;
    readonly Color col;
    readonly Camera cam;
    public bool touchLayout;          // the touch player's HUD: ammo moves to the top-left, away from the FIRE cluster
    Vector2 laidOut = new Vector2(-1, -1);
    bool laidTouch;
    float laidRow = -1f;

    public Hud(Camera cam, string name, Color color, int order)
    {
        col = color;
        this.cam = cam;
        canvas = UIK.MakeCanvas("HUD " + name, cam, order, true);
        Transform r = canvas.transform;

        // balloons (top-left)
        UIK.Img(r, UIK.Circle, new Color(0, 0, 0, 0.35f), new Vector2(0, 1), new Vector2(100, -46), new Vector2(190, 70)).sprite = null;
        for (int i = 0; i < 3; i++)
            balloonIcons[i] = UIK.Img(r, UIK.Circle, color, new Vector2(0, 1), new Vector2(42 + i * 56, -46), new Vector2(44, 52));
        label = UIK.Label(r, name, 26, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(200, -100), new Vector2(380, 34), color);

        // round info (top-center)
        top = UIK.Label(r, "", 24, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(700, 60), Color.white);
        // feed (top-right)
        feed = UIK.Label(r, "", 20, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-245, -112), new Vector2(470, 120), new Color(1, 1, 1, 0.9f));

        // crosshair
        Vector2[] offs = { new Vector2(0, 14), new Vector2(0, -14), new Vector2(14, 0), new Vector2(-14, 0) };
        for (int i = 0; i < 4; i++)
        {
            Vector2 size = (i < 2) ? new Vector2(3, 12) : new Vector2(12, 3);
            crossBars[i] = UIK.Img(r, null, Color.white, new Vector2(0.5f, 0.5f), offs[i], size);
        }
        dot = UIK.Img(r, UIK.Circle, new Color(1f, 0.3f, 0.2f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7, 7));

        pop = UIK.Label(r, "POP!", 54, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(400, 80), new Color(1f, 0.85f, 0.1f));
        hit = UIK.Label(r, "", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(600, 50), new Color(1f, 0.5f, 0.4f));
        center = UIK.Label(r, "", 40, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1000, 240), Color.white);

        // ammo (bottom-right)
        ammo = UIK.Label(r, "", 38, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(-150, 50), new Vector2(280, 60), Color.white);
        pop.enabled = false;
    }

    static void Set(Text t, Vector2 anchor, Vector2 pos, Vector2 box, int size, TextAnchor align)
    {
        RectTransform rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        t.fontSize = size;
        t.alignment = align;
    }

    // Lays the HUD out for this view's shape. Landscape keeps the original layout (balloons top-left, round info top
    // centre, feed top-right, ammo bottom-right); portrait stacks name / ammo / round info / feed down from the top so
    // nothing runs off the narrow screen or under the page's buttons. The touch player's ammo always goes top-left.
    void Layout()
    {
        CanvasScaler sc = canvas.GetComponent<CanvasScaler>();
        Rect pr = cam != null ? cam.pixelRect : new Rect(0, 0, Screen.width, Screen.height);
        bool portrait = pr.height > pr.width * 1.05f;
        Vector2 refRes = portrait ? new Vector2(720, 1280) : new Vector2(1280, 720);
        if (sc != null && sc.referenceResolution != refRes)
        {
            sc.referenceResolution = refRes;
            sc.screenMatchMode = portrait ? CanvasScaler.ScreenMatchMode.Expand : CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        }
        Vector2 size = ((RectTransform)canvas.transform).rect.size;
        // views touching the top edge keep the kill feed below the page toolbar + SOUND button row
        bool atTop = pr.yMax >= Screen.height - 2f;
        float rowPx = atTop ? Mathf.Max(Page.TbH + 6f, Page.T + 58f * Page.Ui) : 0f;
        if (size == laidOut && touchLayout == laidTouch && Mathf.Abs(rowPx - laidRow) < 1f) return;
        laidOut = size; laidTouch = touchLayout; laidRow = rowPx;
        float sf = Mathf.Max(0.05f, canvas.scaleFactor);
        float feedY = -(Mathf.Max(52f, rowPx / sf + 6f) + 60f);
        float W = Mathf.Max(200f, size.x);
        if (portrait)
        {
            Set(top, new Vector2(0.5f, 1), new Vector2(0, -196), new Vector2(W - 30, 70), 22, TextAnchor.UpperCenter);
            Set(feed, new Vector2(0.5f, 1), new Vector2(0, -290), new Vector2(W - 40, 110), 18, TextAnchor.UpperCenter);
        }
        else
        {
            Set(top, new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(Mathf.Min(700, W - 40), 60), 24, TextAnchor.UpperCenter);
            Set(feed, new Vector2(1, 1), new Vector2(-245, feedY), new Vector2(Mathf.Min(470, W * 0.4f), 120), 20, TextAnchor.UpperRight);
        }
        if (touchLayout) Set(ammo, new Vector2(0, 1), new Vector2(110, -146), new Vector2(190, 44), 30, TextAnchor.MiddleLeft);
        else Set(ammo, new Vector2(1, 0), new Vector2(-150, 50), new Vector2(280, 60), 38, TextAnchor.LowerRight);
        center.rectTransform.sizeDelta = new Vector2(Mathf.Min(1000, W - 40), 240);
        hit.rectTransform.sizeDelta = new Vector2(Mathf.Min(600, W - 40), 50);
    }

    public void ShowPop() { popT = 0.7f; }
    public void ShowHit() { hitT = 1.2f; }

    public void Destroy()
    {
        if (canvas != null) Object.Destroy(canvas.gameObject);
    }

    public void Tick(Soldier s, string topText, string centerText, string feedText, bool showCross, float dt)
    {
        if (canvas == null) return;
        Layout();
        for (int i = 0; i < 3; i++)
        {
            bool up = s != null && i < s.balloons.Count && !s.balloons[i].popped;
            balloonIcons[i].sprite = up ? UIK.Circle : UIK.Ring;
            balloonIcons[i].color = up ? col : new Color(1f, 1f, 1f, 0.45f);
        }
        if (s != null)
        {
            if (!s.alive) ammo.text = "";
            else if (s.Reloading) ammo.text = "RELOADING";
            else ammo.text = s.ammo + " / " + Soldier.Mag;
            ammo.color = (s.ammo <= 5 && !s.Reloading) ? new Color(1f, 0.55f, 0.3f) : Color.white;
        }
        top.text = topText;
        center.text = centerText;
        feed.text = feedText;

        float spread = s != null ? Mathf.Lerp(14f, 6f, s.adsBlend) + s.kick * 6f : 14f;
        bool cross = showCross && s != null && s.alive;
        crossBars[0].rectTransform.anchoredPosition = new Vector2(0, spread);
        crossBars[1].rectTransform.anchoredPosition = new Vector2(0, -spread);
        crossBars[2].rectTransform.anchoredPosition = new Vector2(spread, 0);
        crossBars[3].rectTransform.anchoredPosition = new Vector2(-spread, 0);
        for (int i = 0; i < 4; i++) crossBars[i].enabled = cross && (s.adsBlend < 0.8f);
        dot.enabled = cross && s.adsBlend >= 0.5f;

        popT -= dt;
        pop.enabled = popT > 0f;
        if (pop.enabled)
        {
            float k = 1f + Mathf.Clamp01(popT - 0.5f) * 3f;
            pop.rectTransform.localScale = Vector3.one * k;
        }
        hitT -= dt;
        hit.text = hitT > 0f ? (s != null && s.alive ? "Your balloon got popped! " + s.BalloonsLeft + " left" : "") : "";
    }
}
