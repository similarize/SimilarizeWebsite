using UnityEngine;
using UnityEngine.UI;

// On-screen controls for P1 on a touch screen: left stick, drag the free area to look,
// A (hop / get in / out), FIRE, MSL, UP / DOWN (fly), - / + zoom, SND (sound level).
// Two layouts, both inside the device safe area:
//   landscape: stick lower-left, button arc lower-right (as before);
//   portrait:  everything smaller in the bottom band, stick bottom-left, buttons bottom-right, no overlap.
// Look drags use our own per-finger position tracking (no deltaPosition spikes), are clamped per frame,
// and a finger that started on the stick or a button never turns the camera.
public class TouchControls : MonoBehaviour
{
    public bool active;
    public bool extraButtons;        // worlds can show a 2nd row (PHONE etc.) later
    public static bool spaceMode;    // space: A = next target, FIRE = auto-transfer, MSL = land, UP = burn, DOWN = brake
    // ffu14: context buttons. Game sets the labels of A / FIRE / MSL / UP / DOWN for the player's current mode every
    // frame (null = hidden); buttons fade in / out (~0.15 s) and keep their thumb positions. mechMode maps UP / DOWN to
    // jump-rocket / afterburner and FIRE to the chest cannon.
    public static readonly string[] want = { "A", null, null, null, null };
    public static bool mechMode;
    readonly float[] alpha = { 1f, 0f, 0f, 0f, 0f, 1f, 1f, 1f };
    public static string ModeKey { get { return string.Join("|", want); } }
    Canvas canvas;
    RectTransform stickBase, stickKnob;
    int stickId = -1, lookId = -1, upId = -1, downId = -1, fireId = -1;
    Vector2 stickOrigin, stickVec, lookAcc, lookLast;
    bool aQ, fireQ, altQ, resetQ;
    float zoom, lastLookTap = -10f;
    const int Count = 8;
    readonly Image[] imgs = new Image[Count];
    readonly Text[] labels = new Text[Count];
    public static bool Portrait { get { return Screen.height > Screen.width; } }

    static readonly string[] Names = { "A", "FIRE", "MSL", "UP", "DOWN", "-", "+", "SND" };
    static readonly string[] SpaceNames = { "TGT", "AUTO", "LAND", "BURN", "BRAKE", "-", "+", "SND" };
    // anchor 0 = bottom-right, 1 = top-right, 2 = top-left of the safe area; offsets in canvas units
    static readonly int[] Anchor = { 0, 0, 0, 0, 0, 1, 1, 2 };
    static readonly Vector2[] PosL = { new Vector2(-120, 150), new Vector2(-270, 90), new Vector2(-280, 220), new Vector2(-75, 300), new Vector2(-175, 300), new Vector2(-150, -60), new Vector2(-70, -60), new Vector2(60, -150) };
    static readonly float[] RadL = { 80, 58, 44, 44, 44, 30, 30, 30 };
    // portrait: compact cluster, checked for overlap (A r50 / FIRE r38 / MSL r32 / UP r32 / DOWN r32)
    static readonly Vector2[] PosP = { new Vector2(-82, 100), new Vector2(-190, 74), new Vector2(-190, 172), new Vector2(-82, 210), new Vector2(-290, 120), new Vector2(-130, -70), new Vector2(-60, -70), new Vector2(55, -150) };
    static readonly float[] RadP = { 50, 38, 32, 32, 32, 26, 26, 26 };
    static readonly Color[] Cols = { new Color(0.3f, 0.85f, 0.35f, 0.6f), new Color(1f, 0.35f, 0.25f, 0.6f), new Color(1f, 0.7f, 0.2f, 0.55f), new Color(0.4f, 0.8f, 1f, 0.5f), new Color(0.4f, 0.8f, 1f, 0.5f), new Color(1f, 1f, 1f, 0.35f), new Color(1f, 1f, 1f, 0.35f), new Color(1f, 1f, 1f, 0.3f) };
    static readonly Vector2 StickHomeL = new Vector2(170, 170), StickHomeP = new Vector2(112, 118);

    Vector2[] Pos { get { return Portrait ? PosP : PosL; } }
    float[] Rad { get { return Portrait ? RadP : RadL; } }
    Vector2 StickHome { get { return Portrait ? StickHomeP : StickHomeL; } }
    float StickR { get { return Portrait ? 58f : 75f; } }

    // canvas units per pixel: landscape scales with height, portrait with width
    float Scale { get { return Portrait ? Mathf.Clamp(Screen.width / 720f, 0.5f, 2.5f) : Mathf.Clamp(Screen.height / 720f, 0.5f, 2.5f); } }

    Rect Safe { get { Rect r = Screen.safeArea; if (r.width < 10f || r.height < 10f) r = new Rect(0, 0, Screen.width, Screen.height); return r; } }

    // screen position of a control centre
    Vector2 ScreenPos(int i)
    {
        Rect sa = Safe;
        float s = Scale;
        Vector2 o = Pos[i] * s;
        switch (Anchor[i])
        {
            case 1: return new Vector2(sa.xMax + o.x, sa.yMax + o.y);
            case 2: return new Vector2(sa.xMin + o.x, sa.yMax + o.y);
            default: return new Vector2(sa.xMax + o.x, sa.yMin + o.y);
        }
    }

    void Awake()
    {
        canvas = UIK.MakeCanvas("TouchControls", null, 60, false);
        Transform r = canvas.transform;
        stickBase = UIK.Img(r, UIK.Ring, new Color(1, 1, 1, 0.45f), Vector2.zero, StickHomeL, new Vector2(170, 170)).rectTransform;
        stickKnob = UIK.Img(r, UIK.Circle, new Color(1, 1, 1, 0.6f), Vector2.zero, StickHomeL, new Vector2(80, 80)).rectTransform;
        for (int i = 0; i < Count; i++)
        {
            imgs[i] = UIK.Img(r, UIK.Circle, Cols[i], Vector2.zero, Vector2.zero, Vector2.one * 100f);
            labels[i] = UIK.Label(imgs[i].transform, Names[i], 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160, 40), Color.white);
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
            if (i >= 5) labels[i].fontSize = Mathf.RoundToInt((Names[i].Length > 2 ? 0.42f : 0.62f) * rad * (Names[i].Length > 3 ? 0.85f : 1f));
            else if (labels[i].text.Length > 0) { string w = labels[i].text; labels[i].fontSize = Mathf.RoundToInt((w.Length > 2 ? 0.42f : 0.62f) * rad * (w.Length > 4 ? 0.8f : w.Length > 3 ? 0.88f : 1f)); }
            labels[i].rectTransform.sizeDelta = new Vector2(rad * 2.4f, rad);
        }
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        for (int i = 0; i < 5; i++)
        {
            string w = spaceMode ? SpaceNames[i] : want[i];
            if (w != null && labels[i].text != w) { labels[i].text = w; labels[i].fontSize = Mathf.RoundToInt((w.Length > 2 ? 0.42f : 0.62f) * Rad[i] * (w.Length > 4 ? 0.8f : w.Length > 3 ? 0.88f : 1f)); }
            alpha[i] = Mathf.MoveTowards(alpha[i], w != null ? 1f : 0f, dt * 7f);
            Color c = Cols[i]; c.a *= alpha[i];
            imgs[i].color = c;
            labels[i].color = new Color(1f, 1f, 1f, alpha[i]);
            imgs[i].rectTransform.localScale = Vector3.one * (0.8f + 0.2f * alpha[i]);
            bool show = alpha[i] > 0.01f;
            if (imgs[i].gameObject.activeSelf != show) imgs[i].gameObject.SetActive(show);
        }
        labels[7].text = "SND\n<size=" + Mathf.RoundToInt(Rad[7] * 0.38f) + ">" + Sfx.LevelName + "</size>";
        float sr = StickR;
        stickBase.sizeDelta = Vector2.one * sr * 2.25f;
        stickKnob.sizeDelta = Vector2.one * sr * 1.05f;
    }

    int Hit(Vector2 screen)
    {
        float s = Scale;
        for (int i = 0; i < Count; i++)
            if (alpha[i] > 0.5f && (screen - ScreenPos(i)).magnitude < Rad[i] * s * 1.15f) return i;
        return -1;
    }

    Vector2 StickHomeScreen { get { Rect sa = Safe; return new Vector2(sa.xMin, sa.yMin) + StickHome * Scale; } }

    bool InStickZone(Vector2 p)
    {
        Rect sa = Safe;
        if (Portrait) return p.x < sa.xMin + sa.width * 0.5f && p.y < sa.yMin + sa.height * 0.4f;
        return p.x < sa.xMin + sa.width * 0.45f;
    }

    void ResetState()
    {
        stickId = lookId = upId = downId = fireId = -1;
        stickVec = lookAcc = Vector2.zero;
        aQ = fireQ = altQ = resetQ = false;
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
        LayoutControls();
        float s = Scale;
        zoom = 0f;
        int count = Kb.TouchCount();
        bool stickSeen = false, lookSeen = false;
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
                        else if (b == 7) Sfx.CycleVolume();
                        else if (InStickZone(t.position) && stickId < 0) { stickId = t.fingerId; stickOrigin = t.position; stickVec = Vector2.zero; stickSeen = true; }
                        else if (lookId < 0 && !InStickZone(t.position))
                        {
                            lookId = t.fingerId; lookLast = t.position; lookSeen = true;
                            // double-tap the look area = camera reset
                            if (Time.unscaledTime - lastLookTap < 0.3f) resetQ = true;
                            lastLookTap = Time.unscaledTime;
                        }
                        break;
                    }
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (t.fingerId == stickId) { stickVec = Vector2.ClampMagnitude((t.position - stickOrigin) / s / StickR, 1f); stickSeen = true; }
                    else if (t.fingerId == lookId)
                    {
                        Vector2 d = t.position - lookLast;
                        lookLast = t.position;
                        lookAcc += Vector2.ClampMagnitude(d / s, 60f);
                        lookSeen = true;
                    }
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
        // a finger that vanished without an Ended event
        if (stickId >= 0 && !stickSeen && !FingerDown(stickId)) { stickId = -1; stickVec = Vector2.zero; }
        if (lookId >= 0 && !lookSeen && !FingerDown(lookId)) lookId = -1;
        if (count == 0) { stickId = lookId = upId = downId = fireId = -1; stickVec = Vector2.zero; }
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

    public PIn Read()
    {
        var i = new PIn();
        i.move = stickVec;
        i.look = lookAcc * 0.22f;
        i.lookHeld = lookId >= 0;
        lookAcc = Vector2.zero;
        i.hop = i.use = aQ;
        i.fire = fireQ;
        i.fireHeld = fireId >= 0;
        i.alt = altQ;
        i.climb = (upId >= 0 ? 1f : 0f) - (downId >= 0 ? 1f : 0f);
        i.gas = upId >= 0 ? 1f : 0f;
        i.brake = downId >= 0 ? 1f : 0f;
        i.zoom = zoom;
        i.camReset = resetQ;
        if (mechMode) { i.upHeld = upId >= 0; i.boostHeld = downId >= 0; i.gunFire = fireQ; i.gunHeld = fireId >= 0; i.gas = i.brake = 0f; i.climb = i.upHeld ? 1f : 0f; }
        if (spaceMode) { i.target = aQ; i.auto = fireQ; i.land = altQ; i.hop = i.use = false; }
        if (spaceMode) { i.move.y = Mathf.Max(i.move.y, upId >= 0 ? 1f : 0f); }
        aQ = fireQ = altQ = resetQ = false;
        return i;
    }
}
