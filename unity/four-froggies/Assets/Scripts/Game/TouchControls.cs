using UnityEngine;
using UnityEngine.UI;

// Simple on-screen controls for P1 on a touch screen: left stick, drag the right side to look,
// A (hop / get in / out), FIRE, MSL, UP / DOWN (fly), and - / + zoom.
public class TouchControls : MonoBehaviour
{
    public bool active;
    Canvas canvas;
    RectTransform stickBase, stickKnob;
    int stickId = -1, lookId = -1, upId = -1, downId = -1, fireId = -1;
    Vector2 stickOrigin, stickVec, lookAcc;
    bool aQ, fireQ, altQ;
    float zoom;
    readonly Image[] imgs = new Image[7];

    static readonly string[] Names = { "A", "FIRE", "MSL", "UP", "DOWN", "-", "+" };
    static readonly Vector2[] Pos = { new Vector2(-120, 150), new Vector2(-270, 90), new Vector2(-280, 220), new Vector2(-75, 300), new Vector2(-175, 300), new Vector2(-150, -60), new Vector2(-70, -60) };
    static readonly float[] Rad = { 80, 58, 44, 44, 44, 30, 30 };
    static readonly Color[] Cols = { new Color(0.3f, 0.85f, 0.35f, 0.6f), new Color(1f, 0.35f, 0.25f, 0.6f), new Color(1f, 0.7f, 0.2f, 0.55f), new Color(0.4f, 0.8f, 1f, 0.5f), new Color(0.4f, 0.8f, 1f, 0.5f), new Color(1f, 1f, 1f, 0.35f), new Color(1f, 1f, 1f, 0.35f) };
    static readonly Vector2 StickHome = new Vector2(170, 170);

    float Scale { get { return Mathf.Clamp(Screen.height / 720f, 0.5f, 2.5f); } }

    void Awake()
    {
        canvas = UIK.MakeCanvas("TouchControls", null, 60, false);
        Transform r = canvas.transform;
        stickBase = UIK.Img(r, UIK.Ring, new Color(1, 1, 1, 0.45f), Vector2.zero, StickHome, new Vector2(170, 170)).rectTransform;
        stickKnob = UIK.Img(r, UIK.Circle, new Color(1, 1, 1, 0.6f), Vector2.zero, StickHome, new Vector2(80, 80)).rectTransform;
        for (int i = 0; i < Names.Length; i++)
        {
            Vector2 anchor = i >= 5 ? new Vector2(1, 1) : new Vector2(1, 0);
            imgs[i] = UIK.Img(r, UIK.Circle, Cols[i], anchor, Pos[i], Vector2.one * Rad[i] * 2f);
            UIK.Label(imgs[i].transform, Names[i], Names[i].Length > 2 ? 20 : 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Rad[i] * 2f, 40), Color.white);
        }
        canvas.enabled = false;
    }

    int Hit(Vector2 screen)
    {
        float s = Scale;
        for (int i = 0; i < Names.Length; i++)
        {
            Vector2 p = i >= 5 ? new Vector2((screen.x - Screen.width) / s, (screen.y - Screen.height) / s) : new Vector2((screen.x - Screen.width) / s, screen.y / s);
            if ((p - Pos[i]).magnitude < Rad[i] * 1.15f) return i;
        }
        return -1;
    }

    void ResetState()
    {
        stickId = lookId = upId = downId = fireId = -1;
        stickVec = lookAcc = Vector2.zero;
        aQ = fireQ = altQ = false;
        zoom = 0f;
    }

    void Update()
    {
        if (!active)
        {
            if (canvas.enabled) { canvas.enabled = false; ResetState(); }
            return;
        }
        canvas.enabled = true;
        float s = Scale;
        canvas.scaleFactor = s;
        zoom = 0f;
        int count = Kb.TouchCount();
        for (int i = 0; i < count; i++)
        {
            Touch t = Input.GetTouch(i);
            switch (t.phase)
            {
                case TouchPhase.Began:
                    {
                        int b = Hit(t.position);
                        if (b == 0) aQ = true;
                        else if (b == 1) { fireQ = true; fireId = t.fingerId; }
                        else if (b == 2) altQ = true;
                        else if (b == 3) upId = t.fingerId;
                        else if (b == 4) downId = t.fingerId;
                        else if (b == 5 || b == 6) { }
                        else if (t.position.x < Screen.width * 0.45f && stickId < 0) { stickId = t.fingerId; stickOrigin = t.position; }
                        else if (lookId < 0) lookId = t.fingerId;
                        break;
                    }
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (t.fingerId == stickId) stickVec = Vector2.ClampMagnitude((t.position - stickOrigin) / s / 75f, 1f);
                    else if (t.fingerId == lookId) lookAcc += t.deltaPosition / s;
                    else
                    {
                        int b = Hit(t.position);
                        if (b == 5) zoom = -1f;
                        else if (b == 6) zoom = 1f;
                    }
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (t.fingerId == stickId) { stickId = -1; stickVec = Vector2.zero; }
                    if (t.fingerId == lookId) lookId = -1;
                    if (t.fingerId == upId) upId = -1;
                    if (t.fingerId == downId) downId = -1;
                    if (t.fingerId == fireId) fireId = -1;
                    break;
            }
        }
        if (count == 0) { stickId = lookId = upId = downId = fireId = -1; stickVec = Vector2.zero; }
        Vector2 home = stickId >= 0 ? stickOrigin / s : StickHome;
        stickBase.anchoredPosition = home;
        stickKnob.anchoredPosition = home + stickVec * 75f;
    }

    public PIn Read()
    {
        var i = new PIn();
        i.move = stickVec;
        i.look = lookAcc * 0.25f;
        lookAcc = Vector2.zero;
        i.hop = i.use = aQ;
        i.fire = fireQ;
        i.fireHeld = fireId >= 0;
        i.alt = altQ;
        i.climb = (upId >= 0 ? 1f : 0f) - (downId >= 0 ? 1f : 0f);
        i.gas = upId >= 0 ? 1f : 0f;
        i.brake = downId >= 0 ? 1f : 0f;
        i.zoom = zoom;
        aQ = fireQ = altQ = false;
        return i;
    }
}
