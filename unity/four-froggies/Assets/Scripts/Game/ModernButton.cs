using UnityEngine;
using UnityEngine.UI;

// ffu15: modern lobby buttons (HOST / PLAY / JOIN) to match the character showroom: a rounded pill with a soft vertical
// gradient, top sheen, soft coloured glow, drop shadow, crisp generated icon, clean bold label (no retro outline), the
// key hints as small rounded badges, and hover / press / gamepad-focus states. The root Image stays the (transparent)
// hit area, so Game's Hit() tests and the layout code keep working unchanged.
public class ModernButton
{
    public readonly Image root;
    public readonly Text label;
    readonly Image glow, shadow, face, sheen, icon, focusRing, badge;
    readonly Text badgeText;
    Color tint = new Color(0.2f, 0.6f, 0.3f);
    float hoverK, pressK, focusK;
    public bool focus;

    static Sprite pill, softPill;
    static Sprite Pill(bool soft)
    {
        if (!soft && pill != null) return pill;
        if (soft && softPill != null) return softPill;
        const int n = 128; const float R = 62f;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, R + 1f, n - R - 1f), cy = Mathf.Clamp(y + 0.5f, R + 1f, n - R - 1f);
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                float a, v;
                if (soft) { a = Mathf.Clamp01((R + 1f - d) / R); a = a * a * (3f - 2f * a); v = 1f; }      // feathered (glow / shadow)
                else
                {
                    a = Mathf.Clamp01(R - d + 0.5f);
                    float k = y / (n - 1f);
                    v = Mathf.Lerp(0.74f, 1f, k);                                                          // soft vertical gradient
                    if (d > R - 3.5f && y > n * 0.5f) v = Mathf.Min(1f, v + 0.18f);                        // thin top rim highlight
                }
                byte c = (byte)(v * 255f);
                px[y * n + x] = new Color32(c, c, c, (byte)(a * 255f));
            }
        t.SetPixels32(px); t.Apply();
        var sp = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(63, 63, 63, 63));
        if (soft) softPill = sp; else pill = sp;
        return sp;
    }

    // icons drawn once into small alpha textures: 0 play, 1 host (broadcast), 2 join (arrow in), 3 story (rocket, ffu22), else none
    static readonly Sprite[] icons = new Sprite[4];
    public static Sprite Icon(int kind)
    {
        if (kind < 0 || kind > 3) return null;
        if (icons[kind] != null) return icons[kind];
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f, a = 0f;
                if (kind == 0)
                {
                    // rounded play triangle
                    // play triangle: base at u -0.5, apex at u 0.7, half-height 0.72 (distance to the slanted edges ~ normalised)
                    float d = Mathf.Max(-u - 0.5f, (Mathf.Abs(v) - 0.72f * (0.7f - u) / 1.2f) * 0.86f);
                    a = Mathf.Clamp01(0.5f - d * n * 0.5f);
                }
                else if (kind == 1)
                {
                    // broadcast: dot + two arcs (top half fan)
                    float r = Mathf.Sqrt(u * u + (v + 0.35f) * (v + 0.35f));
                    float ang = Mathf.Abs(Mathf.Atan2(u, v + 0.35f)) * Mathf.Rad2Deg;
                    float dot = Mathf.Clamp01((0.17f - r) * n * 0.5f);
                    float arc1 = ang < 50f ? Mathf.Clamp01((0.075f - Mathf.Abs(r - 0.48f)) * n * 0.5f) : 0f;
                    float arc2 = ang < 50f ? Mathf.Clamp01((0.075f - Mathf.Abs(r - 0.86f)) * n * 0.5f) : 0f;
                    a = Mathf.Max(dot, Mathf.Max(arc1, arc2));
                }
                else if (kind == 3)
                {
                    // ffu22 story: a little rocket leaning right - pointed body, window hole, two fins, flame
                    float ru = u * 0.8f + v * 0.6f, rv = -u * 0.6f + v * 0.8f;      // rotate -37 deg
                    float body = (Mathf.Abs(ru) < 0.22f * Mathf.Clamp01((0.78f - rv) / 0.3f) && rv > -0.45f && rv < 0.78f) ? 1f : 0f;
                    float win = (ru * ru + (rv - 0.22f) * (rv - 0.22f) < 0.0075f) ? 1f : 0f;
                    float fin = (rv > -0.55f && rv < -0.12f && Mathf.Abs(ru) < 0.42f - (rv + 0.55f) * 0.45f && Mathf.Abs(ru) > 0.18f) ? 1f : 0f;
                    float fl = (rv < -0.5f && rv > -0.92f && Mathf.Abs(ru) < 0.13f * (rv + 0.92f) / 0.42f) ? 0.85f : 0f;
                    a = Mathf.Max(Mathf.Max(body * (1f - win), fin), fl);
                }
                else
                {
                    // join: arrow pointing into an open bracket
                    float shaft = (Mathf.Abs(v) < 0.1f && u > -0.85f && u < 0.05f) ? 1f : 0f;
                    float head = (u >= 0.0f && u < 0.42f && Mathf.Abs(v) < (0.42f - u) * 1.05f) ? 1f : 0f;
                    float br = ((u > 0.55f && u < 0.75f && Mathf.Abs(v) < 0.85f) || (Mathf.Abs(Mathf.Abs(v) - 0.75f) < 0.1f && u > 0.25f && u < 0.75f)) ? 1f : 0f;
                    a = Mathf.Max(shaft, Mathf.Max(head, br));
                }
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        icons[kind] = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return icons[kind];
    }

    static Image Child(Transform p, Sprite s, Color c, bool sliced)
    {
        var im = UIK.Img(p, s, c, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        if (sliced) im.type = Image.Type.Sliced;
        return im;
    }

    public ModernButton(Transform parent, string text, int iconKind, Color c)
    {
        root = UIK.Img(parent, null, new Color(1f, 1f, 1f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 72));
        Transform r = root.transform;
        glow = Child(r, Pill(true), c, true);
        UIK.Anchor(glow.rectTransform, Vector2.zero, Vector2.one, new Vector2(-18, -18), new Vector2(18, 18));
        shadow = Child(r, Pill(true), new Color(0f, 0f, 0f, 0.45f), true);
        UIK.Anchor(shadow.rectTransform, Vector2.zero, Vector2.one, new Vector2(-6, -14), new Vector2(6, 0));
        focusRing = Child(r, Pill(false), new Color(1f, 1f, 1f, 0f), true);
        UIK.Anchor(focusRing.rectTransform, Vector2.zero, Vector2.one, new Vector2(-5, -5), new Vector2(5, 5));
        face = Child(r, Pill(false), c, true);
        UIK.Stretch(face.rectTransform);
        sheen = Child(r, Pill(false), new Color(1f, 1f, 1f, 0.16f), true);
        UIK.Anchor(sheen.rectTransform, new Vector2(0f, 0.52f), Vector2.one, new Vector2(6, 0), new Vector2(-6, -4));
        icon = UIK.Img(r, Icon(iconKind), Color.white, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(30, 30));
        icon.preserveAspect = true;
        if (icon.sprite == null) icon.enabled = false;
        label = UIK.Label(r, text, 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.white);
        Object.Destroy(label.GetComponent<Outline>());
        var sh = label.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.35f); sh.effectDistance = new Vector2(0f, -2f);
        label.supportRichText = true;
        label.resizeTextForBestFit = true; label.resizeTextMinSize = 12; label.resizeTextMaxSize = 34;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        badge = Child(r, UIK.Round, new Color(0f, 0f, 0f, 0.28f), true);
        badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        badgeText = UIK.Label(badge.transform, "", 14, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.92f));
        Object.Destroy(badgeText.GetComponent<Outline>());
        badgeText.fontStyle = FontStyle.Bold;
        UIK.Stretch(badgeText.rectTransform);
        tint = c;
    }

    public void SetTint(Color c) { tint = c; }
    public void SetBadge(string s) { if (badgeText.text != s) badgeText.text = s; badge.gameObject.SetActive(s.Length > 0); }

    // per frame: hover / press from the mouse, focus from the lobby, layout from the root's current size
    public void Tick(bool mouseAllowed)
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        bool hover = false, press = false;
        if (mouseAllowed && root.gameObject.activeInHierarchy && UnityEngine.InputSystem.Mouse.current != null)
        {
            Vector2 mp = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            hover = RectTransformUtility.RectangleContainsScreenPoint(root.rectTransform, mp, null);
            press = hover && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed;
        }
        if (Kb.TouchCount() > 0) for (int i = 0; i < Input.touchCount; i++) if (RectTransformUtility.RectangleContainsScreenPoint(root.rectTransform, Input.GetTouch(i).position, null)) press = true;
        hoverK = Mathf.MoveTowards(hoverK, hover ? 1f : 0f, dt * 8f);
        pressK = Mathf.MoveTowards(pressK, press ? 1f : 0f, dt * 14f);
        focusK = Mathf.MoveTowards(focusK, focus ? 1f : 0f, dt * 6f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.5f);
        float s = 1f + 0.035f * hoverK - 0.04f * pressK + 0.015f * focusK * pulse;
        root.rectTransform.localScale = new Vector3(s, s, 1f);
        Color f = Color.Lerp(tint, Color.white, 0.08f * hoverK);
        f = Color.Lerp(f, Color.black, 0.12f * pressK); f.a = tint.a;
        face.color = f;
        Color g = tint; g.a = 0.28f + 0.25f * hoverK + 0.3f * focusK * (0.6f + 0.4f * pulse);
        glow.color = g;
        focusRing.color = new Color(1f, 1f, 1f, 0.85f * focusK);
        shadow.rectTransform.offsetMin = new Vector2(-6, -14 + 6 * pressK);
        // layout: sliced images shrink their borders to fit, so the 63 px corners become exact pill ends at any height;
        // icon + label + badge spacing scale with the button
        float h = Mathf.Max(20f, root.rectTransform.rect.height), w = root.rectTransform.rect.width;
        float ic = icon.enabled ? h * 0.42f : 0f;
        icon.rectTransform.sizeDelta = new Vector2(ic, ic);
        icon.rectTransform.anchoredPosition = new Vector2(h * 0.42f + ic * 0.5f, 0f);
        bool bOn = badge.gameObject.activeSelf;
        float bw = bOn ? Mathf.Max(44f, badgeText.preferredWidth + 18f) : 0f, bh = Mathf.Min(30f, h * 0.42f);
        badge.rectTransform.sizeDelta = new Vector2(bw, bh);
        badge.rectTransform.anchoredPosition = new Vector2(-h * 0.3f - bw * 0.5f, 0f);
        badgeText.fontSize = Mathf.RoundToInt(bh * 0.52f);
        float left = icon.enabled ? h * 0.42f + ic + 8f : h * 0.4f, right = bOn ? h * 0.3f + bw + 8f : h * 0.4f;
        UIK.Anchor(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(left, 4f), new Vector2(-right, -4f));
        label.resizeTextMaxSize = Mathf.RoundToInt(h * 0.42f);
    }
}
