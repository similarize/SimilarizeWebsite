using UnityEngine;
using UnityEngine.UI;

// Tiny uGUI construction kit (no editor, no prefabs).
public static class UIK
{
    static Font font;
    static Sprite circle, ring;

    public static Font Font
    {
        get
        {
            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                if (font == null) { try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            }
            return font;
        }
    }

    static Sprite MakeCircle(bool hollow)
    {
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(c - d);
                if (hollow) a = Mathf.Min(a, Mathf.Clamp01(d - (c - 6f)));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        t.SetPixels32(px);
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    public static Sprite Circle { get { if (circle == null) circle = MakeCircle(false); return circle; } }
    public static Sprite Ring { get { if (ring == null) ring = MakeCircle(true); return ring; } }

    public static Canvas MakeCanvas(string name, Camera cam, int order, bool scaleWithScreen)
    {
        var go = new GameObject(name);
        var c = go.AddComponent<Canvas>();
        if (cam != null)
        {
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = cam;
            c.planeDistance = 0.2f;
        }
        else c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        var s = go.AddComponent<CanvasScaler>();
        if (scaleWithScreen)
        {
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1280, 720);
            s.matchWidthOrHeight = 0.6f;
        }
        else s.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        return c;
    }

    public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static Text Label(Transform parent, string text, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box, Color col)
    {
        RectTransform rt = Rect(parent, "Text", anchor, pos, box);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font;
        t.fontSize = size;
        t.alignment = align;
        t.color = col;
        t.text = text;
        t.fontStyle = FontStyle.Bold;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var o = rt.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0, 0, 0, 0.75f);
        o.effectDistance = new Vector2(2, -2);
        return t;
    }

    public static Image Img(Transform parent, Sprite sp, Color col, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        RectTransform rt = Rect(parent, "Img", anchor, pos, size);
        var i = rt.gameObject.AddComponent<Image>();
        i.sprite = sp;
        i.color = col;
        i.raycastTarget = false;
        return i;
    }

    // ffu14: 9-sliced rounded rectangle (lobby cards, tiles, buttons)
    static Sprite round;
    public static Sprite Round
    {
        get
        {
            if (round != null) return round;
            const int n = 64; const float R = 20f;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, R, n - R), cy = Mathf.Clamp(y + 0.5f, R, n - R);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(R - d + 0.5f) * 255));
                }
            t.SetPixels32(px);
            t.Apply();
            round = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
            return round;
        }
    }
    public static Image Panel(Transform parent, Color col, Vector2 pos, Vector2 size)
    {
        Image i = Img(parent, Round, col, new Vector2(0.5f, 0.5f), pos, size);
        i.type = Image.Type.Sliced;
        return i;
    }
    // vertical gradient strip (top colour -> bottom colour), stretched over its rect
    public static Sprite Gradient(Color top, Color mid, Color bottom)
    {
        const int n = 64;
        var t = new Texture2D(1, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < n; y++)
        {
            float k = y / (n - 1f);
            t.SetPixel(0, y, k > 0.5f ? Color.Lerp(mid, top, (k - 0.5f) * 2f) : Color.Lerp(bottom, mid, k * 2f));
        }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, 1, n), new Vector2(0.5f, 0.5f), 100f);
    }
    public static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
