using System.Collections.Generic;
using UnityEngine;

// Boat gate course on the pond: bobbing red/green buoy pairs with a hoop, taken in order for a timed lap.
// The next gate for each boat glows; passing one chimes, a full lap plays the win jingle and shows the time.
public class PondCourse : MonoBehaviour
{
    class Gate { public Vector3 c; public Transform root; public Renderer hoop; public Quaternion rot; }
    class Run { public int next; public float start = -1f; }

    readonly List<Gate> gates = new List<Gate>();
    readonly Dictionary<Vehicle, Run> runs = new Dictionary<Vehicle, Run>();
    float best = -1f;
    Material hoopIdle, hoopNext;

    public static void Create()
    {
        var go = new GameObject("PondCourse");
        go.AddComponent<PondCourse>().Build();
    }

    void Build()
    {
        hoopIdle = Mats.Lit(new Color(1f, 0.85f, 0.2f));
        hoopNext = Mats.Unlit(new Color(0.3f, 1f, 0.45f));
        Vector2 c = Layout.PondC, r = Layout.PondR;
        Vector3 ramp = new Vector3(c.x - 22f, 0f, c.y + 18f);
        for (int i = 0; i < 14 && gates.Count < 8; i++)
        {
            float a = i * Mathf.PI * 2f / 14f + 0.2f;
            Vector3 p = new Vector3(c.x + Mathf.Cos(a) * r.x * 0.78f, Layout.WaterY, c.y + Mathf.Sin(a) * r.y * 0.78f);
            bool ok = (new Vector2(p.x - ramp.x, p.z - ramp.z)).magnitude > 14f;
            foreach (Vector3 isl in Layout.Islands) if (new Vector2(p.x - isl.x, p.z - isl.y).magnitude < isl.z + 9f) ok = false;
            if (!ok) continue;
            Vector3 tan = new Vector3(-Mathf.Sin(a) * r.x, 0f, Mathf.Cos(a) * r.y).normalized;
            gates.Add(MakeGate(p, tan, gates.Count + 1));
        }
    }

    Gate MakeGate(Vector3 p, Vector3 tan, int number)
    {
        var g = new Gate { c = p };
        g.root = new GameObject("Gate " + number).transform;
        g.root.position = p;
        g.rot = Quaternion.LookRotation(tan, Vector3.up);
        g.root.rotation = g.rot;
        Transform t = g.root;
        Mats.Prim(PrimitiveType.Sphere, t, new Vector3(-3.5f, 0.2f, 0f), Vector3.one * 1.1f, Mats.Shiny(new Color(0.95f, 0.15f, 0.12f)));
        Mats.Prim(PrimitiveType.Sphere, t, new Vector3(3.5f, 0.2f, 0f), Vector3.one * 1.1f, Mats.Shiny(new Color(0.1f, 0.8f, 0.25f)));
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, t, new Vector3(3.5f * s, 2f, 0f), new Vector3(0.18f, 3.4f, 0.18f), Mats.Lit(Color.white));
        var top = Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 3.75f, 0f), new Vector3(7.2f, 0.35f, 0.35f), hoopIdle);
        g.hoop = top.GetComponent<Renderer>();
        Ranch.Sign(p + Vector3.up * 5f, Mathf.Atan2(tan.x, tan.z) * Mathf.Rad2Deg + 180f, number.ToString(), new Color(0.1f, 0.2f, 0.5f), 1.6f, 1.4f);
        return g;
    }

    void Update()
    {
        float t = Time.time;
        for (int i = 0; i < gates.Count; i++)
        {
            Gate g = gates[i];
            Vector3 p = g.c;
            p.y = Layout.WaterY + Mathf.Sin(t * 1.3f + i) * 0.12f;
            g.root.position = p;
            g.root.rotation = g.rot * Quaternion.Euler(Mathf.Sin(t * 0.9f + i) * 2f, 0f, Mathf.Sin(t * 1.1f + i * 2f) * 2f);
            g.hoop.sharedMaterial = hoopIdle;
        }
        if (gates.Count == 0) return;
        foreach (Vehicle v in Vehicle.All)
        {
            if (!(v is Boat) || v.driver == null) continue;
            Run run;
            if (!runs.TryGetValue(v, out run)) { run = new Run(); runs[v] = run; }
            Gate g = gates[run.next];
            if (v.driver.human) g.hoop.sharedMaterial = hoopNext;
            Vector3 d = v.transform.position - g.c; d.y = 0f;
            if (d.magnitude < 4.2f)
            {
                if (run.next == 0)
                {
                    if (run.start > 0f && Time.time - run.start > 5f)
                    {
                        float lap = Time.time - run.start;
                        bool rec = best < 0f || lap < best;
                        if (rec) best = lap;
                        v.driver.Toast("LAP " + lap.ToString("0.0") + " s" + (rec ? "  NEW BEST!" : "  (best " + best.ToString("0.0") + " s)"), 4f);
                        if (v.driver.human) Sfx.Play(Sfx.Win, 0.8f);
                    }
                    else v.driver.Toast("Gate course: GO! Through all " + gates.Count + " hoops", 3f);
                    run.start = Time.time;
                }
                else
                {
                    v.driver.Toast("Gate " + (run.next + 1) + " / " + gates.Count, 1.5f);
                }
                if (v.driver.human) Sfx.Play(Sfx.Pickup, 0.6f, 1f + run.next * 0.04f);
                FX.Splash(g.c + Vector3.up * 0.3f, 8);
                run.next = (run.next + 1) % gates.Count;
            }
        }
    }
}
