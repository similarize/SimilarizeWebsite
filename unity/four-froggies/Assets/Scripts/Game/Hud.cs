using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// One HUD panel per viewport: player title, prompt, vehicle status, and floating name tags for every froggy.
public class ViewHud
{
    public RectTransform panel;
    readonly Text title, prompt, status, center;
    readonly Text[] tags = new Text[4];
    readonly Image frame;

    public ViewHud(Transform canvas, string name)
    {
        panel = UIK.Rect(canvas, "View " + name, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIK.Stretch(panel);
        frame = UIK.Img(panel, null, new Color(0f, 0f, 0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIK.Stretch(frame.rectTransform);
        var ol = frame.gameObject.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.8f);
        ol.effectDistance = new Vector2(2f, 2f);
        title = UIK.Label(panel, "", 24, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(170f, -26f), new Vector2(320f, 40f), Color.white);
        status = UIK.Label(panel, "", 20, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(170f, 26f), new Vector2(320f, 40f), Color.white);
        prompt = UIK.Label(panel, "", 22, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(760f, 70f), new Color(1f, 0.95f, 0.6f));
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

    // me == null: shared view (list every player in the status corner)
    public void Tick(Camera cam, Frog me, string playerTag, List<Frog> frogs, string sharedStatus)
    {
        frame.color = new Color(0f, 0f, 0f, 0f);
        if (me != null)
        {
            title.text = playerTag + "  " + me.nick;
            title.color = me.color;
            prompt.text = me.prompt;
            Vehicle v = me.vehicle;
            if (v != null)
            {
                string s = v.Title + "  " + Mathf.RoundToInt(v.Speed * 3.6f) + " km/h";
                if (v.flyer) s += "  alt " + Mathf.RoundToInt(Mathf.Max(0f, v.transform.position.y)) + " m";
                status.text = s;
            }
            else status.text = "";
        }
        else
        {
            title.text = "";
            prompt.text = "";
            status.text = sharedStatus;
        }
        for (int i = 0; i < tags.Length; i++)
        {
            Text t = tags[i];
            if (cam == null || i >= frogs.Count || frogs[i] == null) { t.enabled = false; continue; }
            Frog f = frogs[i];
            Vector3 wp = f.FocusPoint + Vector3.up * (f.vehicle != null ? 2.2f : 1.0f);
            Vector3 sp = cam.WorldToScreenPoint(wp);
            if (sp.z < 0.5f || !cam.pixelRect.Contains(new Vector2(sp.x, sp.y)) || (f == me && f.vehicle == null)) { t.enabled = false; continue; }
            Vector2 lp;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, sp, null, out lp);
            t.enabled = true;
            t.text = f.nick + (f.human ? "" : " (AI)");
            t.color = f.color;
            t.fontSize = sp.z > 60f ? 15 : 20;
            t.rectTransform.anchoredPosition = lp;
        }
    }
}
