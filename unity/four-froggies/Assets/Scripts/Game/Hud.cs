using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// One HUD panel per viewport: player title, prompt, vehicle status, and floating name tags for every froggy.
public class ViewHud
{
    public RectTransform panel;
    readonly Text title, prompt, status, center, toast;
    float inset = -1f;
    readonly Text[] tags = new Text[4];
    readonly Image frame, fadeImg;
    // ffu15: mech aim reticle + space radar
    readonly RectTransform reticle;
    readonly Image[] retParts = new Image[6];
    readonly SpaceRadar radar;

    public ViewHud(Transform canvas, string name)
    {
        panel = UIK.Rect(canvas, "View " + name, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIK.Stretch(panel);
        frame = UIK.Img(panel, null, new Color(0f, 0f, 0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIK.Stretch(frame.rectTransform);
        var ol = frame.gameObject.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.8f);
        ol.effectDistance = new Vector2(2f, 2f);
        fadeImg = UIK.Img(panel, null, new Color(0f, 0f, 0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);   // launch fade (under the text)
        UIK.Stretch(fadeImg.rectTransform);
        fadeImg.enabled = false;
        title = UIK.Label(panel, "", 24, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(170f, -26f), new Vector2(320f, 40f), Color.white);
        status = UIK.Label(panel, "", 20, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(170f, 26f), new Vector2(320f, 40f), Color.white);
        prompt = UIK.Label(panel, "", 22, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(760f, 70f), new Color(1f, 0.95f, 0.6f));
        toast = UIK.Label(panel, "", 26, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(760f, 80f), new Color(0.6f, 1f, 0.6f));
        center = UIK.Label(panel, "", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 160f), Color.white);
        for (int i = 0; i < 4; i++)
        {
            tags[i] = UIK.Label(panel, "", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 30f), Color.white);
        }
        prompt.supportRichText = true;
        reticle = UIK.Rect(panel, "Reticle", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));
        retParts[0] = UIK.Img(reticle, UIK.Ring, new Color(1f, 1f, 1f, 0.9f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 46f));
        retParts[1] = UIK.Img(reticle, UIK.Circle, new Color(1f, 1f, 1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
        retParts[2] = UIK.Img(reticle, null, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 33f), new Vector2(3f, 16f));
        retParts[3] = UIK.Img(reticle, null, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, -33f), new Vector2(3f, 16f));
        retParts[4] = UIK.Img(reticle, null, Color.white, new Vector2(0.5f, 0.5f), new Vector2(33f, 0f), new Vector2(16f, 3f));
        retParts[5] = UIK.Img(reticle, null, Color.white, new Vector2(0.5f, 0.5f), new Vector2(-33f, 0f), new Vector2(16f, 3f));
        foreach (var im in retParts) { var sh = im.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.7f); sh.effectDistance = new Vector2(1.5f, -1.5f); }
        reticle.gameObject.SetActive(false);
        radar = new SpaceRadar(panel);
    }

    public void SetActive(bool on) { panel.gameObject.SetActive(on); }

    // ffu21: TV static over a mech pilot's view while the mech's head (cockpit cameras) is blown off, and the small
    // "hold toward the tree" climb progress ring
    RawImage staticImg; Text staticText;
    Image climbRing, climbBack;
    static Texture2D noiseTex;
    void Ffu21Overlays(Frog me)
    {
        StoryMech sm = me != null ? me.vehicle as StoryMech : null;
        bool blind = sm != null && sm.HeadLost;
        if (blind && staticImg == null)
        {
            if (noiseTex == null)
            {
                noiseTex = new Texture2D(128, 128, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
                var px = new Color32[128 * 128];
                var r = new System.Random(3);
                for (int i = 0; i < px.Length; i++) { byte v = (byte)r.Next(256); px[i] = new Color32(v, v, v, 255); }
                noiseTex.SetPixels32(px); noiseTex.Apply();
            }
            var go = new GameObject("Static", typeof(RectTransform));
            go.transform.SetParent(panel, false);
            go.transform.SetSiblingIndex(2);
            staticImg = go.AddComponent<RawImage>();
            staticImg.texture = noiseTex;
            staticImg.raycastTarget = false;
            UIK.Stretch(staticImg.rectTransform);
            staticText = UIK.Label(panel, "COCKPIT CAMERAS DOWN\n<size=20>head blown off - it grows back soon</size>", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(700f, 120f), new Color(1f, 0.4f, 0.35f));
            staticText.supportRichText = true;
        }
        if (staticImg != null)
        {
            if (staticImg.gameObject.activeSelf != blind) { staticImg.gameObject.SetActive(blind); staticText.gameObject.SetActive(blind); }
            if (blind)
            {
                float w = Mathf.Max(1f, panel.rect.width / 3f), h = Mathf.Max(1f, panel.rect.height / 3f);
                staticImg.uvRect = new Rect(Random.value, Random.value, w / 128f, h / 128f);
                staticImg.color = new Color(1f, 1f, 1f, 0.78f + 0.12f * Mathf.Sin(Time.time * 23f));
                staticText.color = new Color(1f, 0.4f, 0.35f, Mathf.Repeat(Time.time, 1f) < 0.6f ? 1f : 0.35f);
            }
        }
        float cp = me != null ? me.climbProgress : 0f;
        if (cp > 0.01f && climbRing == null)
        {
            climbBack = UIK.Img(panel, UIK.Ring, new Color(0f, 0f, 0f, 0.45f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(64f, 64f));
            climbRing = UIK.Img(panel, UIK.Ring, new Color(0.55f, 1f, 0.55f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(64f, 64f));
            climbRing.type = Image.Type.Filled; climbRing.fillMethod = Image.FillMethod.Radial360; climbRing.fillOrigin = (int)Image.Origin360.Top; climbRing.fillClockwise = true;
            climbRing.raycastTarget = climbBack.raycastTarget = false;
        }
        if (climbRing != null)
        {
            bool on = cp > 0.01f;
            if (climbRing.gameObject.activeSelf != on) { climbRing.gameObject.SetActive(on); climbBack.gameObject.SetActive(on); }
            if (on) climbRing.fillAmount = Mathf.Clamp01(cp);
        }
    }

    public void SetRect(Rect r)
    {
        panel.anchorMin = new Vector2(r.xMin, r.yMin);
        panel.anchorMax = new Vector2(r.xMax, r.yMax);
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
    }

    public void SetCenter(string s) { center.text = s; }

    public void SetFade(float a)
    {
        bool on = a > 0.001f;
        if (fadeImg.enabled != on) fadeImg.enabled = on;
        if (on) fadeImg.color = new Color(0f, 0f, 0f, Mathf.Clamp01(a));
    }

    // bottom band reserved for touch controls (canvas units); 0 = none
    public void SetBottomInset(float units, float width)
    {
        if (Mathf.Abs(units - inset) < 0.5f) return;
        inset = units;
        prompt.rectTransform.anchoredPosition = new Vector2(0f, 64f + units);
        // ffu14e: on a full-width landscape view keep the (long, mech) prompt clear of the status corner (x 10..330)
        prompt.rectTransform.sizeDelta = new Vector2(width >= 1100f ? Mathf.Min(760f, width - 680f) : Mathf.Min(760f, width - 20f), 70f);
        toast.rectTransform.anchoredPosition = new Vector2(0f, 120f + units);
        toast.rectTransform.sizeDelta = new Vector2(Mathf.Min(760f, width - 20f), 80f);
        status.rectTransform.anchoredPosition = new Vector2(170f, 26f + units);
    }

    // me == null: shared view (list every player in the status corner)
    public void Tick(Camera cam, Frog me, string playerTag, List<Frog> frogs, string sharedStatus, string sharedPrompt = null, Frog sharedSpace = null)
    {
        frame.color = new Color(0f, 0f, 0f, 0f);
        Ffu21Overlays(me);
        // ffu15 reticle: over-the-shoulder aim in a mech (own view only); red over a target, pulses with recoil
        StoryMech am = me != null ? me.vehicle as StoryMech : null;
        bool ret = am != null && am.aimK > 0.25f;
        if (reticle.gameObject.activeSelf != ret) reticle.gameObject.SetActive(ret);
        if (ret)
        {
            Color rc = am.aimOnTarget ? new Color(1f, 0.3f, 0.25f, 1f) : new Color(1f, 1f, 1f, 0.95f);
            rc.a *= Mathf.Clamp01((am.aimK - 0.25f) * 3f);
            foreach (var im in retParts) im.color = rc;
            float sp = 1f + am.recoilK * 0.5f;
            reticle.localScale = Vector3.one * sp;
            for (int k = 2; k < 6; k++) { Vector2 d = k == 2 ? Vector2.up : k == 3 ? Vector2.down : k == 4 ? Vector2.right : Vector2.left; retParts[k].rectTransform.anchoredPosition = d * (33f + am.recoilK * 10f); }
        }
        radar.Tick(me != null ? me : sharedSpace, cam, panel.rect.width < 900f);
        if (me != null)
        {
            title.text = playerTag + "  " + me.nick;
            title.color = me.color;
            prompt.text = me.prompt;
            toast.text = me.toastT > 0f ? me.toast : "";
            Vehicle v = me.vehicle != null ? me.vehicle : me.passengerOf;
            if (v != null)
            {
                string s = v.Title + "  " + Mathf.RoundToInt(v.Speed * 3.6f) + " km/h";
                if (me.world == WorldId.Underwater) s += "  depth " + Mathf.RoundToInt(Mathf.Max(0f, Worlds.UnderO.y - v.transform.position.y)) + " m";
                else if (v is StoryMech) s += "  " + ((StoryMech)v).StatusLine;
                else if (v.flyer) s += "  alt " + Mathf.RoundToInt(Mathf.Max(0f, v.transform.position.y)) + " m";
                status.text = s;
            }
            else status.text = me.world == WorldId.Underwater ? "Scuba  depth " + Mathf.RoundToInt(Mathf.Max(0f, Worlds.UnderO.y - me.transform.position.y)) + " m  |  pearls " + (Pickups.Total("pearls") - Pickups.Remaining("pearls")) + " / " + Pickups.Total("pearls") : (me.world != WorldId.Ranch ? Worlds.NameAt(me.world, me.transform.position) : "");
        }
        else
        {
            title.text = "";
            prompt.text = sharedPrompt ?? "";
            string tl = "";
            foreach (Frog f in frogs) if (f != null && f.human && f.toastT > 0f) tl += (tl.Length > 0 ? "\n" : "") + "<color=#" + ColorUtility.ToHtmlStringRGB(f.color) + ">" + f.nick + "</color>: " + f.toast;
            toast.text = tl;
            status.text = sharedStatus;
        }
        // ffu9: keep the toast clear of a tall (multi-line) prompt, e.g. the space controls line
        float ins = Mathf.Max(0f, inset);
        float ph = prompt.text.Length > 0 ? prompt.preferredHeight : 0f;
        float ty = Mathf.Max(120f, 64f + ph + 10f) + ins;
        if (Mathf.Abs(toast.rectTransform.anchoredPosition.y - ty) > 0.5f) toast.rectTransform.anchoredPosition = new Vector2(0f, ty);
        for (int i = 0; i < tags.Length; i++)
        {
            Text t = tags[i];
            if (cam == null || i >= frogs.Count || frogs[i] == null) { t.enabled = false; continue; }
            Frog f = frogs[i];
            Vector3 wp = f.FocusPoint + Vector3.up * (f.vehicle != null ? 2.2f : 1.0f);
            Vector3 sp = cam.WorldToScreenPoint(wp);
            if (sp.z < 0.5f || sp.z > 500f || !cam.pixelRect.Contains(new Vector2(sp.x, sp.y)) || (f == me && f.vehicle == null) || (me != null && f.world != me.world) || f.launching || f.passengerOf != null) { t.enabled = false; continue; }   // ffu27: no tags for froggies hidden inside the Starship
            Vector2 lp;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, sp, null, out lp);
            t.enabled = true;
            bool online = f.netPuppet && Net.I != null && Net.I.remoteHuman[f.id];   // ffu13: another device's player
            t.text = f.nick + (f.human || online ? "" : " (AI)");
            t.color = f.color;
            t.fontSize = sp.z > 60f ? 15 : 20;
            t.rectTransform.anchoredPosition = lp;
        }
    }
}
