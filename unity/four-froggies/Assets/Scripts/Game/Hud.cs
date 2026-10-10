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
    }

    public void SetActive(bool on) { panel.gameObject.SetActive(on); }

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
    public void Tick(Camera cam, Frog me, string playerTag, List<Frog> frogs, string sharedStatus)
    {
        frame.color = new Color(0f, 0f, 0f, 0f);
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
            else status.text = me.world == WorldId.Underwater ? "Scuba  depth " + Mathf.RoundToInt(Mathf.Max(0f, Worlds.UnderO.y - me.transform.position.y)) + " m  |  pearls " + (Pickups.Total("pearls") - Pickups.Remaining("pearls")) + " / " + Pickups.Total("pearls") : (me.world != WorldId.Ranch ? Worlds.Name(me.world) : "");
        }
        else
        {
            title.text = "";
            prompt.text = "";
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
            if (sp.z < 0.5f || sp.z > 500f || !cam.pixelRect.Contains(new Vector2(sp.x, sp.y)) || (f == me && f.vehicle == null) || (me != null && f.world != me.world)) { t.enabled = false; continue; }
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
