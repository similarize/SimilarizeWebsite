using UnityEngine;
using UnityEngine.UI;

// Tiny uGUI construction kit (no editor, no prefabs).
public static class UIK
{
    static Font font;
    static Sprite circle, ring;

    // ffu20 (Bill: "the font throughout the WHOLE game should look modern, smooth, anti-aliased"): every UI Text and
    // world TextMesh now uses the ffu17 faces - Inter SemiBold (UIK.Font, body) / Montserrat ExtraBold (UIK.Display,
    // titles >= 24 px and world labels) - with soft drop shadows instead of the hard 2 px outlines. The old built-in
    // LegacyRuntime font is only the fallback (UIK.LegacyFont) if the TTFs are missing.
    public static Font Font { get { LoadFonts(); return body != null ? body : LegacyFont; } }
    public static Font WorldFont { get { LoadFonts(); return display != null ? display : LegacyFont; } }

    // world TextMesh: modern face, glyphs rasterised at k x the size (characterSize / k keeps the world size), bilinear
    // filtered font atlas; call after fontSize / characterSize are set ("<size=..>" tags must be scaled by k too)
    public static void HiRes(TextMesh tm, int k = 2)
    {
        if (tm == null) return;
        Font f = WorldFont;
        tm.font = f;
        tm.fontStyle = FontStyle.Normal;
        tm.fontSize = Mathf.Min(256, tm.fontSize * k);
        tm.characterSize /= k;
        var mr = tm.GetComponent<MeshRenderer>();
        if (mr != null && (mr.sharedMaterial == null || mr.sharedMaterial.shader == null || mr.sharedMaterial.shader.name.Contains("GUI/Text") || mr.sharedMaterial.name.Contains("Font"))) mr.sharedMaterial = f.material;
        if (f.material != null && f.material.mainTexture != null) f.material.mainTexture.filterMode = FilterMode.Bilinear;
    }
    public static void WorldText(TextMesh tm, int size, float charSize, int k = 2) { tm.fontSize = size; tm.characterSize = charSize; HiRes(tm, k); }

    static Font legacy;
    public static Font LegacyFont
    {
        get
        {
            if (legacy != null) return legacy;
            Font font = null;
            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                if (font == null) { try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            }
            legacy = font;
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
        LoadFonts();
        bool modern = display != null && body != null;
        t.font = modern ? (size >= 24 ? display : body) : LegacyFont;
        t.fontSize = size;
        t.alignment = align;
        t.color = col;
        t.text = text;
        t.fontStyle = modern ? FontStyle.Normal : FontStyle.Bold;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        if (modern)
        {
            // ffu20: soft two-layer drop shadow (keeps text readable over the bright ranch without the hard outline)
            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.5f); sh.effectDistance = new Vector2(0f, -1.5f);
            var sh2 = rt.gameObject.AddComponent<Shadow>();
            sh2.effectColor = new Color(0f, 0f, 0f, 0.22f); sh2.effectDistance = new Vector2(1.2f, -3f);
        }
        else
        {
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.75f);
            o.effectDistance = new Vector2(2, -2);
        }
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

    // ---------- ffu17 modern lobby kit ----------
    // Modern UI fonts (SIL OFL 1.1, Latin subsets in Resources/Fonts): Montserrat ExtraBold for display text, Inter SemiBold
    // for body text. Dynamic TTF fonts are rasterised by FreeType at the real on-screen pixel size (fontSize x canvas
    // scale), so with a DPR-correct canvas the glyphs are 1:1 and crisp; the weight is in the face, so no faux bold.
    static Font display, body;
    static bool fontsTried;
    static void LoadFonts()
    {
        if (fontsTried) return;
        fontsTried = true;
        try { display = Resources.Load<Font>("Fonts/FFDisplay"); } catch { }
        try { body = Resources.Load<Font>("Fonts/FFBody"); } catch { }
        Debug.Log("UIK: modern fonts display " + (display != null) + " body " + (body != null));
    }
    public static Font Display { get { LoadFonts(); return display != null ? display : LegacyFont; } }
    public static Font Body { get { LoadFonts(); return body != null ? body : LegacyFont; } }
    public static bool ModernFonts { get { LoadFonts(); return display != null && body != null; } }

    // restyle one label: modern face, no faux bold, soft drop shadow instead of the hard retro outline
    public static void Modernize(Text t, bool displayFace, float shadowA = 0.42f)
    {
        if (t == null) return;
        LoadFonts();
        Font f = displayFace ? display : body;
        if (f == null) return;
        t.font = f;
        t.fontStyle = FontStyle.Normal;
        foreach (var o in t.GetComponents<Shadow>()) Object.DestroyImmediate(o);   // Outline derives from Shadow
        Shadow sh = null;
        if (sh == null && shadowA > 0f) sh = t.gameObject.AddComponent<Shadow>();
        if (sh != null) { sh.effectColor = new Color(0f, 0f, 0f, shadowA); sh.effectDistance = new Vector2(0f, -2f); }
    }

    // hi-res rounded rect (radius 20 design units, 2x texels via pixelsPerUnitMultiplier), stays smooth on 2.5x screens
    static Sprite round2;
    public static Sprite Round2
    {
        get
        {
            if (round2 != null) return round2;
            const int n = 128; const float R = 40f;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, R, n - R), cy = Mathf.Clamp(y + 0.5f, R, n - R);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(R - d + 0.5f) * 255));
                }
            t.SetPixels32(px); t.Apply();
            round2 = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(48, 48, 48, 48));
            return round2;
        }
    }
    // 1-px-wide (2 texels) rounded outline ring, 9-sliced like Round2 (glass card edge)
    static Sprite edge2;
    public static Sprite Edge2
    {
        get
        {
            if (edge2 != null) return edge2;
            const int n = 128; const float R = 40f, W = 2.2f;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, R, n - R), cy = Mathf.Clamp(y + 0.5f, R, n - R);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    float a = Mathf.Clamp01(R - d + 0.5f) * Mathf.Clamp01(d - (R - W) + 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply();
            edge2 = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(48, 48, 48, 48));
            return edge2;
        }
    }
    // feathered rounded rect: soft glow / shadow behind cards (9-sliced, the falloff lives in the border)
    static Sprite soft;
    public static Sprite SoftRect
    {
        get
        {
            if (soft != null) return soft;
            const int n = 128; const float R = 60f;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, R + 2f, n - R - 2f), cy = Mathf.Clamp(y + 0.5f, R + 2f, n - R - 2f);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    float a = Mathf.Clamp01((R - d) / R); a = a * a * (3f - 2f * a);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply();
            soft = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(62, 62, 62, 62));
            return soft;
        }
    }
    // radial soft dot (bokeh / glow), gaussian-ish falloff
    static Sprite dot;
    public static Sprite SoftDot
    {
        get
        {
            if (dot != null) return dot;
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply();
            dot = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return dot;
        }
    }
    public static Image Glass(Transform parent, Color col, Vector2 pos, Vector2 size)
    {
        Image i = Img(parent, Round2, col, new Vector2(0.5f, 0.5f), pos, size);
        i.type = Image.Type.Sliced;
        i.pixelsPerUnitMultiplier = 2f;
        return i;
    }
    public static Image Sliced(Transform parent, Sprite sp, Color col, float ppuMul)
    {
        Image i = Img(parent, sp, col, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        i.type = Image.Type.Sliced;
        i.pixelsPerUnitMultiplier = ppuMul;
        return i;
    }
}

// ffu17: vertical colour gradient over a whole Text / Graphic (top -> bottom), applied to the vertex colours
public class UIGradient : BaseMeshEffect
{
    public Color top = Color.white, bottom = Color.white;
    readonly System.Collections.Generic.List<UIVertex> verts = new System.Collections.Generic.List<UIVertex>();
    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;
        verts.Clear();
        vh.GetUIVertexStream(verts);
        float lo = float.MaxValue, hi = float.MinValue;
        for (int i = 0; i < verts.Count; i++) { float y = verts[i].position.y; if (y < lo) lo = y; if (y > hi) hi = y; }
        float h = Mathf.Max(0.001f, hi - lo);
        for (int i = 0; i < verts.Count; i++)
        {
            UIVertex v = verts[i];
            Color k = Color.Lerp(bottom, top, (v.position.y - lo) / h);
            Color32 o = v.color;
            v.color = new Color(k.r, k.g, k.b, k.a * (o.a / 255f));
            verts[i] = v;
        }
        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }
}
