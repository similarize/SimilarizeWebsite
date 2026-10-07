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

    public Hud(Camera cam, string name, Color color, int order)
    {
        col = color;
        canvas = UIK.MakeCanvas("HUD " + name, cam, order, true);
        Transform r = canvas.transform;

        // balloons (top-left)
        UIK.Img(r, UIK.Circle, new Color(0, 0, 0, 0.35f), new Vector2(0, 1), new Vector2(100, -46), new Vector2(190, 70)).sprite = null;
        for (int i = 0; i < 3; i++)
            balloonIcons[i] = UIK.Img(r, UIK.Circle, color, new Vector2(0, 1), new Vector2(42 + i * 56, -46), new Vector2(44, 52));
        label = UIK.Label(r, name, 26, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(110, -100), new Vector2(200, 34), color);

        // round info (top-center)
        top = UIK.Label(r, "", 24, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(700, 60), Color.white);
        // feed (top-right)
        feed = UIK.Label(r, "", 20, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-190, -70), new Vector2(360, 120), new Color(1, 1, 1, 0.9f));

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

    public void ShowPop() { popT = 0.7f; }
    public void ShowHit() { hitT = 1.2f; }

    public void Destroy()
    {
        if (canvas != null) Object.Destroy(canvas.gameObject);
    }

    public void Tick(Soldier s, string topText, string centerText, string feedText, bool showCross, float dt)
    {
        if (canvas == null) return;
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
