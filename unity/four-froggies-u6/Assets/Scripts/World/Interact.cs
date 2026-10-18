using System.Collections.Generic;
using UnityEngine;

// Things a froggy on foot can use: doors, fish tanks, toy boxes, people, the submarine, the Starship...
// "auto" hotspots (doorways) fire as soon as a human frog walks into them.
public class Hotspot
{
    public Vector3 pos;
    public float radius = 2f;
    public string label = "";
    public bool auto;
    public bool humansOnly = true;
    public System.Action<Frog> act;
    public System.Func<Frog, bool> enabled;
    public System.Func<Frog, string> dynLabel;
    public string Label(Frog f) { return dynLabel != null ? dynLabel(f) : label; }
}

public static class Interact
{
    public static readonly List<Hotspot> All = new List<Hotspot>();

    public static Hotspot Add(Vector3 p, float r, string label, System.Action<Frog> act, bool auto = false)
    {
        var h = new Hotspot { pos = p, radius = r, label = label, act = act, auto = auto };
        All.Add(h);
        return h;
    }

    public static Hotspot Nearest(Vector3 p, Frog f, out float dist)
    {
        Hotspot best = null; dist = 1e9f;
        foreach (Hotspot h in All)
        {
            if (h.humansOnly && !f.human) continue;
            if (h.enabled != null && !h.enabled(f)) continue;
            Vector3 d = h.pos - p;
            if (Mathf.Abs(d.y) > 3f) continue;
            d.y = 0f;
            float m = d.magnitude;
            if (m < h.radius && m < dist) { dist = m; best = h; }
        }
        return best;
    }
}
