using UnityEngine;
using UnityEngine.UI;

// Phone controls for P1 (Pixel 9 etc.): a floating steering stick on the left, GAS / BRAKE / FIRE / DRIFT
// on the right, all inside the safe area. There is no camera control on touch at all, so a finger on the
// stick (or anywhere else) can never move the camera. Taps on the global SOUND / pause buttons are ignored.
public class TouchControls : MonoBehaviour
{
    public bool active;
    Canvas canvas;
    RectTransform stickBase, stickKnob;
    int stickId = -1, gasId = -1, brakeId = -1, driftId = -1;
    Vector2 stickOrigin, stickVec;
    bool fireQ;
    const int Count = 4;
    readonly Image[] imgs = new Image[Count];
    readonly Text[] labels = new Text[Count];
    static readonly string[] Names = { "GAS", "BRAKE", "FIRE", "DRIFT" };
    static readonly Color[] Cols = { new Color(0.25f, 0.85f, 0.3f, 0.55f), new Color(0.95f, 0.3f, 0.25f, 0.5f), new Color(1f, 0.6f, 0.15f, 0.6f), new Color(0.35f, 0.75f, 1f, 0.5f) };
    // offsets from the bottom-right of the safe area (canvas units)
    static readonly Vector2[] PosL = { new Vector2(-115, 125), new Vector2(-285, 80), new Vector2(-265, 235), new Vector2(-110, 300) };
    static readonly float[] RadL = { 88, 54, 62, 52 };
    static readonly Vector2[] PosP = { new Vector2(-90, 110), new Vector2(-235, 70), new Vector2(-215, 200), new Vector2(-85, 250) };
    static readonly float[] RadP = { 70, 46, 52, 44 };
    public static bool Portrait { get { return Screen.height > Screen.width; } }
    Vector2[] Pos { get { return Portrait ? PosP : PosL; } }
    float[] Rad { get { return Portrait ? RadP : RadL; } }
    float StickR { get { return Portrait ? 62f : 78f; } }
    Vector2 StickHome { get { return Portrait ? new Vector2(115, 125) : new Vector2(175, 165); } }
    float Scale { get { return Portrait ? Mathf.Clamp(Screen.width / 720f, 0.5f, 2.5f) : Mathf.Clamp(Screen.height / 720f, 0.5f, 2.5f); } }
    Rect Safe { get { Rect r = Screen.safeArea; if (r.width < 10f || r.height < 10f) r = new Rect(0, 0, Screen.width, Screen.height); return r; } }

    // canvas units the HUD should keep clear at the bottom
    public float BottomBand { get { return Portrait ? 340f : 0f; } }

    Vector2 ScreenPos(int i)
    {
        Rect sa = Safe;
        Vector2 o = Pos[i] * Scale;
        return new Vector2(sa.xMax + o.x, sa.yMin + o.y);
    }

    Vector2 StickHomeScreen { get { Rect sa = Safe; return new Vector2(sa.xMin, sa.yMin) + StickHome * Scale; } }

    void Awake()
    {
        canvas = UIK.MakeCanvas("TouchControls", null, 60, false);
        Transform r = canvas.transform;
        stickBase = UIK.Img(r, UIK.Ring, new Color(1, 1, 1, 0.45f), Vector2.zero, Vector2.zero, new Vector2(170, 170)).rectTransform;
        stickKnob = UIK.Img(r, UIK.Circle, new Color(1, 1, 1, 0.6f), Vector2.zero, Vector2.zero, new Vector2(80, 80)).rectTransform;
        for (int i = 0; i < Count; i++)
        {
            imgs[i] = UIK.Img(r, UIK.Circle, Cols[i], Vector2.zero, Vector2.zero, Vector2.one * 100f);
            labels[i] = UIK.Label(imgs[i].transform, Names[i], 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160, 40), Color.white);
        }
        canvas.enabled = false;
    }

    void LayoutControls()
    {
        float s = Scale;
        canvas.scaleFactor = s;
        for (int i = 0; i < Count; i++)
        {
            float rad = Rad[i];
            imgs[i].rectTransform.anchoredPosition = ScreenPos(i) / s;
            imgs[i].rectTransform.sizeDelta = Vector2.one * rad * 2f;
            labels[i].fontSize = Mathf.RoundToInt(rad * (Names[i].Length > 3 ? 0.36f : 0.44f));
            labels[i].rectTransform.sizeDelta = new Vector2(rad * 2.4f, rad);
        }
        imgs[0].color = gasId >= 0 ? new Color(0.35f, 1f, 0.4f, 0.8f) : Cols[0];
        imgs[1].color = brakeId >= 0 ? new Color(1f, 0.4f, 0.35f, 0.8f) : Cols[1];
        imgs[3].color = driftId >= 0 ? new Color(0.5f, 0.85f, 1f, 0.8f) : Cols[3];
        stickBase.sizeDelta = Vector2.one * StickR * 2.25f;
        stickKnob.sizeDelta = Vector2.one * StickR * 1.05f;
    }

    int Hit(Vector2 screen)
    {
        float s = Scale;
        int best = -1; float bd = 1e9f;
        for (int i = 0; i < Count; i++)
        {
            float d = (screen - ScreenPos(i)).magnitude;
            if (d < Rad[i] * s * 1.2f && d < bd) { bd = d; best = i; }
        }
        return best;
    }

    bool InStickZone(Vector2 p)
    {
        Rect sa = Safe;
        if (Portrait) return p.x < sa.xMin + sa.width * 0.45f && p.y < sa.yMin + sa.height * 0.45f;
        return p.x < sa.xMin + sa.width * 0.45f && p.y < sa.yMin + sa.height * 0.8f;
    }

    void ResetState()
    {
        stickId = gasId = brakeId = driftId = -1;
        stickVec = Vector2.zero;
        fireQ = false;
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
        int count = Kb.TouchCount();
        bool stickSeen = false;
        for (int i = 0; i < count; i++)
        {
            Touch t = Input.GetTouch(i);
            switch (t.phase)
            {
                case TouchPhase.Began:
                    {
                        if (Sfx.ButtonHit(t.position) || Game.PauseButtonHit(t.position)) break;
                        int b = Hit(t.position);
                        if (b == 0) gasId = t.fingerId;
                        else if (b == 1) brakeId = t.fingerId;
                        else if (b == 2) fireQ = true;
                        else if (b == 3) driftId = t.fingerId;
                        else if (InStickZone(t.position) && stickId < 0) { stickId = t.fingerId; stickOrigin = t.position; stickVec = Vector2.zero; stickSeen = true; }
                        break;
                    }
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (t.fingerId == stickId) { stickVec = Vector2.ClampMagnitude((t.position - stickOrigin) / s / StickR, 1f); stickSeen = true; }
                    else if (t.fingerId == gasId || t.fingerId == brakeId)
                    {
                        // slide between GAS and BRAKE without lifting
                        int b = Hit(t.position);
                        if (b == 0 && t.fingerId == brakeId) { brakeId = -1; gasId = t.fingerId; }
                        else if (b == 1 && t.fingerId == gasId) { gasId = -1; brakeId = t.fingerId; }
                    }
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (t.fingerId == stickId) { stickId = -1; stickVec = Vector2.zero; }
                    if (t.fingerId == gasId) gasId = -1;
                    if (t.fingerId == brakeId) brakeId = -1;
                    if (t.fingerId == driftId) driftId = -1;
                    break;
            }
        }
        if (stickId >= 0 && !stickSeen && !FingerDown(stickId)) { stickId = -1; stickVec = Vector2.zero; }
        if (count == 0) { stickId = gasId = brakeId = driftId = -1; stickVec = Vector2.zero; }
        LayoutControls();
        Vector2 home = stickId >= 0 ? stickOrigin / s : StickHomeScreen / s;
        stickBase.anchoredPosition = home;
        stickKnob.anchoredPosition = home + stickVec * StickR;
    }

    static bool FingerDown(int id)
    {
        int n = Kb.TouchCount();
        for (int i = 0; i < n; i++) if (Input.GetTouch(i).fingerId == id) return true;
        return false;
    }

    public KIn Read()
    {
        var i = new KIn();
        float x = stickVec.x;
        i.steer = Mathf.Abs(x) < 0.08f ? 0f : Mathf.Sign(x) * Mathf.Clamp01((Mathf.Abs(x) - 0.08f) / 0.8f);
        i.gas = gasId >= 0 ? 1f : 0f;
        i.brake = brakeId >= 0 ? 1f : 0f;
        i.fire = fireQ;
        i.drift = driftId >= 0;
        fireQ = false;
        return i;
    }
}
