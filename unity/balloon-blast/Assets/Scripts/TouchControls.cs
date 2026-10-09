using UnityEngine;
using UnityEngine.UI;

// On-screen touch controls, laid out in screen pixels inside the touch player's own view (full screen, or its
// split-screen rect) minus the safe-area insets, re-laid out whenever that rect changes (rotate, resize, fullscreen).
//  - move stick: bottom-left; a touch anywhere in the lower-left zone starts it right there (floating stick)
//  - FIRE: large button at the right thumb; ADS / RELOAD / JUMP on an arc around it, never overlapping it or each other
//  - look: drag anywhere else in the view (the right side, the top, and also while holding FIRE)
// Portrait scales everything to the narrow side and keeps the stick and the button cluster apart.
// Uses the legacy touch API (active input handling is set to Both).
public class TouchControls : MonoBehaviour
{
    public bool active;
    public Rect view = new Rect(0, 0, 1, 1);   // touch player's camera pixel rect (set by Game every frame)
    public static bool DemoPress;               // demo/probe: draw FIRE as held so screenshots show the pressed state

    class Btn { public string id, label; public Vector2 c; public float r; public Image img; public Text txt; public Color col; }

    Canvas canvas;
    RectTransform stickBase, stickKnob, hintRt;
    Text hint;
    Btn fire, ads, jump, reload;
    Btn[] btns;
    int stickId = -1, lookId = -1, fireId = -1;
    Vector2 stickOrigin, stickVec, lookAcc, stickHome;
    float stickR, u;
    bool adsOn, jumpQ, reloadQ;
    Rect laidOut = new Rect(-1, -1, -1, -1);
    float shownT;
    public bool Portrait { get; private set; }
    public Rect Area { get; private set; }      // safe part of the view, in px

    void Awake()
    {
        canvas = UIK.MakeCanvas("TouchControls", null, 60, false);
        canvas.GetComponent<CanvasScaler>().scaleFactor = 1f;
        Transform r = canvas.transform;
        stickBase = UIK.Img(r, UIK.Ring, new Color(1, 1, 1, 0.45f), Vector2.zero, Vector2.zero, Vector2.one).rectTransform;
        stickKnob = UIK.Img(r, UIK.Circle, new Color(1, 1, 1, 0.6f), Vector2.zero, Vector2.zero, Vector2.one).rectTransform;
        fire = MakeBtn(r, "fire", "FIRE", new Color(1f, 0.35f, 0.25f, 0.62f));
        ads = MakeBtn(r, "ads", "AIM", new Color(1f, 1f, 1f, 0.42f));
        jump = MakeBtn(r, "jump", "JUMP", new Color(0.4f, 0.8f, 1f, 0.5f));
        reload = MakeBtn(r, "reload", "RELOAD", new Color(1f, 0.85f, 0.3f, 0.5f));
        btns = new[] { fire, ads, jump, reload };
        hint = UIK.Label(r, "drag here to look", 20, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, new Vector2(300, 40), new Color(1, 1, 1, 0.75f));
        hintRt = hint.rectTransform;
        canvas.enabled = false;
    }

    Btn MakeBtn(Transform parent, string id, string label, Color c)
    {
        var b = new Btn { id = id, label = label, col = c };
        b.img = UIK.Img(parent, UIK.Circle, c, Vector2.zero, Vector2.zero, Vector2.one * 10f);
        b.txt = UIK.Label(b.img.transform, label, 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10, 10), Color.white);
        b.txt.raycastTarget = false;
        b.txt.resizeTextForBestFit = false;
        return b;
    }

    static void Place(RectTransform rt, Vector2 c, float diameter)
    {
        rt.anchoredPosition = c;
        rt.sizeDelta = new Vector2(diameter, diameter);
    }

    // all sizes in u = short side of the view / 400 (a phone's short side is ~400 CSS px)
    void Layout(Rect v)
    {
        Area = Page.Safe(v);
        Rect a = Area;
        float w = a.width, h = a.height;
        Portrait = h > w * 1.1f;
        u = Mathf.Clamp(Mathf.Min(w, h) / 400f, 0.4f, 3.2f);
        float m = 14f * u;                                   // edge margin
        float fr = (Portrait ? 44f : 48f) * u;               // FIRE radius
        float sr = (Portrait ? 27f : 30f) * u;               // small button radius
        float gap = 13f * u;
        fire.r = fr; ads.r = sr; jump.r = sr; reload.r = sr;
        fire.c = new Vector2(a.xMax - m - fr - 8f * u, a.yMin + m + fr + (Portrait ? 30f : 20f) * u);
        float d = fr + sr + gap;
        jump.c = fire.c + Polar(Portrait ? 86f : 80f, d);
        reload.c = fire.c + Polar(Portrait ? 136f : 130f, d);
        ads.c = fire.c + Polar(Portrait ? 188f : 194f, d);
        // keep the arc inside the area (very short split-screen views)
        foreach (var b in btns)
        {
            b.c.y = Mathf.Clamp(b.c.y, a.yMin + b.r + 4f, a.yMax - b.r - 4f);
            b.c.x = Mathf.Clamp(b.c.x, a.xMin + b.r + 4f, a.xMax - b.r - 4f);
        }
        stickR = (Portrait ? 56f : 62f) * u;
        stickHome = new Vector2(a.xMin + m + stickR + 8f * u, a.yMin + m + stickR + (Portrait ? 34f : 22f) * u);
        foreach (var b in btns)
        {
            Place(b.img.rectTransform, b.c, b.r * 2f);
            b.txt.rectTransform.sizeDelta = new Vector2(b.r * 2.2f, b.r);
            b.txt.fontSize = Mathf.Max(8, Mathf.RoundToInt((b == fire ? 0.42f : (b.label.Length > 4 ? 0.3f : 0.4f)) * b.r));
        }
        stickBase.sizeDelta = Vector2.one * stickR * 2f;
        stickKnob.sizeDelta = Vector2.one * stickR * 0.95f;
        hint.fontSize = Mathf.Max(8, Mathf.RoundToInt(15f * u));
        hintRt.sizeDelta = new Vector2(240f * u, 30f * u);
        // the hint sits in the open look area: above the cluster on the right side
        hintRt.anchoredPosition = new Vector2(Portrait ? a.center.x + w * 0.18f : a.xMin + w * 0.68f, Portrait ? a.yMin + h * 0.52f : a.yMin + h * 0.6f);
        laidOut = v;
        Debug.Log("TouchControls layout " + (Portrait ? "portrait" : "landscape") + " view " + v + " safe " + a + " u=" + u.ToString("0.00") +
                  " fire " + fire.c + " r" + fr.ToString("0") + " stick " + stickHome + " r" + stickR.ToString("0"));
    }

    static Vector2 Polar(float deg, float d) { float r = deg * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * d; }

    Btn HitButton(Vector2 p)
    {
        Btn best = null; float bd = float.MaxValue;
        foreach (var b in btns)
        {
            float dd = (p - b.c).magnitude;
            if (dd < b.r * 1.12f && dd < bd) { bd = dd; best = b; }
        }
        return best;
    }

    // lower-left zone that starts the floating stick
    bool InStickZone(Vector2 p)
    {
        Rect a = Area;
        if (!a.Contains(p)) return false;
        float zx = a.xMin + a.width * (Portrait ? 0.5f : 0.42f);
        float zy = a.yMin + a.height * (Portrait ? 0.5f : 0.8f);
        return p.x < zx && p.y < zy;
    }

    void ResetState()
    {
        stickId = lookId = fireId = -1;
        stickVec = Vector2.zero; lookAcc = Vector2.zero;
        jumpQ = reloadQ = false;
    }

    void Update()
    {
        if (!active)
        {
            if (canvas.enabled) { canvas.enabled = false; ResetState(); adsOn = false; }
            shownT = 0f;
            return;
        }
        if (!canvas.enabled) { canvas.enabled = true; shownT = 0f; }
        shownT += Time.unscaledDeltaTime;
        Page.Refresh();
        Rect v = view;
        if (v.width < 2f || v.height < 2f) v = new Rect(0, 0, Screen.width, Screen.height);
        if (v != laidOut || Page.Safe(v) != Area) Layout(v);

        int count = Kb.TouchCount();
        for (int i = 0; i < count; i++)
        {
            Touch t = Input.GetTouch(i);
            switch (t.phase)
            {
                case TouchPhase.Began:
                    {
                        if (Sfx.ButtonHit(t.position)) break;   // SOUND button (handled by SfxDriver)
                        if (!v.Contains(t.position)) break;     // another player's split-screen view
                        Btn b = HitButton(t.position);
                        if (b == fire) { fireId = t.fingerId; }
                        else if (b == ads) { adsOn = !adsOn; Sfx.UiTap(); }
                        else if (b == jump) jumpQ = true;
                        else if (b == reload) reloadQ = true;
                        else if (stickId < 0 && InStickZone(t.position))
                        {
                            stickId = t.fingerId;
                            // start where the thumb landed, but keep the ring on screen
                            Rect a = Area;
                            stickOrigin = new Vector2(Mathf.Clamp(t.position.x, a.xMin + stickR, a.xMax - stickR), Mathf.Clamp(t.position.y, a.yMin + stickR, a.yMax - stickR));
                        }
                        else if (lookId < 0) lookId = t.fingerId;
                        break;
                    }
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (t.fingerId == stickId)
                    {
                        Vector2 d = t.position - stickOrigin;
                        stickVec = Vector2.ClampMagnitude(d / (stickR * 0.85f), 1f);
                    }
                    else if (t.fingerId == lookId || t.fingerId == fireId)
                    {
                        lookAcc += t.deltaPosition / Mathf.Max(0.4f, u);
                    }
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (t.fingerId == stickId) { stickId = -1; stickVec = Vector2.zero; }
                    if (t.fingerId == lookId) lookId = -1;
                    if (t.fingerId == fireId) fireId = -1;
                    break;
            }
        }
        if (count == 0) { stickId = lookId = fireId = -1; stickVec = Vector2.zero; }

        Vector2 home = stickId >= 0 ? stickOrigin : stickHome;
        stickBase.anchoredPosition = home;
        stickKnob.anchoredPosition = home + stickVec * stickR * 0.85f;
        bool fireDown = fireId >= 0 || DemoPress;
        fire.img.color = fireDown ? new Color(1f, 0.3f, 0.2f, 0.92f) : fire.col;
        fire.img.rectTransform.localScale = Vector3.one * (fireDown ? 0.94f : 1f);
        ads.img.color = adsOn ? new Color(0.5f, 1f, 0.5f, 0.78f) : ads.col;
        // "drag here to look" for the first few seconds of a match
        float ha = Mathf.Clamp01(6f - shownT) * 0.8f;
        hint.enabled = ha > 0.01f;
        hint.color = new Color(1, 1, 1, ha);
    }

    public void Read(out Vector2 move, out Vector2 lookDeg, out bool fireOut, out bool adsOut, out bool jumpOut, out bool reloadOut)
    {
        move = stickVec;
        lookDeg = lookAcc * 0.3f;   // ~0.3 deg per CSS px of drag in either orientation
        lookAcc = Vector2.zero;
        fireOut = fireId >= 0;
        adsOut = adsOn;
        jumpOut = jumpQ;
        reloadOut = reloadQ;
        jumpQ = reloadQ = false;
    }
}
