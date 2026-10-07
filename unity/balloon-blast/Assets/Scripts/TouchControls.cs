using UnityEngine;
using UnityEngine.UI;

// On-screen touch controls: left virtual stick, right-side drag to look, FIRE / ADS / JUMP / RELOAD buttons.
// Uses the legacy touch API (active input handling is set to Both).
public class TouchControls : MonoBehaviour
{
    public bool active;
    Canvas canvas;
    RectTransform stickBase, stickKnob;
    Image fireImg, adsImg, jumpImg, reloadImg;
    int stickId = -1, lookId = -1, fireId = -1;
    Vector2 stickOrigin, stickVec, lookAcc;
    bool adsOn, jumpQ, reloadQ;

    static readonly Vector2 FirePos = new Vector2(-140, 170), AdsPos = new Vector2(-300, 95), JumpPos = new Vector2(-110, 340), ReloadPos = new Vector2(-290, 250);
    const float FireR = 85, SmallR = 55;
    static readonly Vector2 StickHome = new Vector2(170, 170);

    float Scale { get { return Mathf.Clamp(Screen.height / 720f, 0.5f, 2.5f); } }

    void Awake()
    {
        canvas = UIK.MakeCanvas("TouchControls", null, 60, false);
        Transform r = canvas.transform;
        stickBase = UIK.Img(r, UIK.Ring, new Color(1, 1, 1, 0.45f), Vector2.zero, StickHome, new Vector2(170, 170)).rectTransform;
        stickKnob = UIK.Img(r, UIK.Circle, new Color(1, 1, 1, 0.6f), Vector2.zero, StickHome, new Vector2(80, 80)).rectTransform;
        fireImg = Button(r, "FIRE", FirePos, FireR, new Color(1f, 0.35f, 0.25f, 0.6f));
        adsImg = Button(r, "ADS", AdsPos, SmallR, new Color(1f, 1f, 1f, 0.4f));
        jumpImg = Button(r, "JUMP", JumpPos, SmallR, new Color(0.4f, 0.8f, 1f, 0.5f));
        reloadImg = Button(r, "RELOAD", ReloadPos, SmallR, new Color(1f, 0.85f, 0.3f, 0.5f));
        canvas.enabled = false;
    }

    Image Button(Transform parent, string text, Vector2 pos, float radius, Color c)
    {
        Image img = UIK.Img(parent, UIK.Circle, c, new Vector2(1, 0), pos, Vector2.one * radius * 2f);
        Text t = UIK.Label(img.transform, text, text.Length > 4 ? 20 : 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(radius * 2f, 40), Color.white);
        t.raycastTarget = false;
        return img;
    }

    string HitButton(Vector2 screen)
    {
        float s = Scale;
        Vector2 p = new Vector2((screen.x - Screen.width) / s, screen.y / s);
        if ((p - FirePos).magnitude < FireR * 1.1f) return "fire";
        if ((p - AdsPos).magnitude < SmallR * 1.15f) return "ads";
        if ((p - JumpPos).magnitude < SmallR * 1.15f) return "jump";
        if ((p - ReloadPos).magnitude < SmallR * 1.15f) return "reload";
        return null;
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
            return;
        }
        canvas.enabled = true;
        float s = Scale;
        canvas.scaleFactor = s;

        int count = Kb.TouchCount();
        for (int i = 0; i < count; i++)
        {
            Touch t = Input.GetTouch(i);
            switch (t.phase)
            {
                case TouchPhase.Began:
                    {
                        string b = HitButton(t.position);
                        if (b == "fire") fireId = t.fingerId;
                        else if (b == "ads") adsOn = !adsOn;
                        else if (b == "jump") jumpQ = true;
                        else if (b == "reload") reloadQ = true;
                        else if (t.position.x < Screen.width * 0.45f && stickId < 0) { stickId = t.fingerId; stickOrigin = t.position; }
                        else if (lookId < 0) lookId = t.fingerId;
                        break;
                    }
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (t.fingerId == stickId)
                    {
                        Vector2 d = (t.position - stickOrigin) / s;
                        stickVec = Vector2.ClampMagnitude(d / 75f, 1f);
                    }
                    else if (t.fingerId == lookId || t.fingerId == fireId)
                    {
                        lookAcc += t.deltaPosition / s;
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

        Vector2 home = stickId >= 0 ? stickOrigin / s : StickHome;
        stickBase.anchoredPosition = home;
        stickKnob.anchoredPosition = home + stickVec * 75f;
        fireImg.color = fireId >= 0 ? new Color(1f, 0.3f, 0.2f, 0.9f) : new Color(1f, 0.35f, 0.25f, 0.6f);
        adsImg.color = adsOn ? new Color(0.5f, 1f, 0.5f, 0.75f) : new Color(1f, 1f, 1f, 0.4f);
    }

    public void Read(out Vector2 move, out Vector2 lookDeg, out bool fire, out bool ads, out bool jump, out bool reload)
    {
        move = stickVec;
        lookDeg = lookAcc * 0.22f;
        lookAcc = Vector2.zero;
        fire = fireId >= 0;
        ads = adsOn;
        jump = jumpQ;
        reload = reloadQ;
        jumpQ = reloadQ = false;
    }
}
