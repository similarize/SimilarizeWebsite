using System.Collections.Generic;
using UnityEngine;

// Collectibles shared by the worlds (pearls, crystals, ice shards...): they bob and spin, and a human frog
// (on foot or in its vehicle) that gets close in the same world picks them up.
public class Pickups : MonoBehaviour
{
    public class Item { public Transform t; public Vector3 basePos; public WorldId world; public string group; public bool taken; public float radius = 1.8f; public float spin = 90f; }
    static Pickups inst;
    readonly List<Item> items = new List<Item>();
    public static System.Action<Item, Frog> OnCollect;
    static readonly List<System.Action<Item, Frog>> handlers = new List<System.Action<Item, Frog>>();

    static Pickups I { get { if (inst == null) inst = new GameObject("Pickups").AddComponent<Pickups>(); return inst; } }

    public static void Listen(System.Action<Item, Frog> h) { handlers.Add(h); }

    public static Item Add(Transform t, WorldId w, string group, float radius = 1.8f)
    {
        var it = new Item { t = t, basePos = t.position, world = w, group = group, radius = radius };
        I.items.Add(it);
        return it;
    }

    public static int Remaining(string group) { int n = 0; foreach (var it in I.items) if (it.group == group && !it.taken) n++; return n; }
    public static int Total(string group) { int n = 0; foreach (var it in I.items) if (it.group == group) n++; return n; }

    public static void Reset(string group)
    {
        foreach (var it in I.items) if (it.group == group) { it.taken = false; it.t.gameObject.SetActive(true); }
    }

    void Update()
    {
        if (Game.I == null) return;
        float t = Time.time;
        foreach (Item it in items)
        {
            if (it.taken) continue;
            bool watched = false;
            foreach (Frog f in Game.I.frogs) if (f != null && f.human && f.world == it.world) watched = true;
            if (!watched) continue;
            it.t.position = it.basePos + Vector3.up * Mathf.Sin(t * 2f + it.basePos.x) * 0.15f;
            it.t.Rotate(0f, it.spin * Time.deltaTime, 0f, Space.World);
            foreach (Frog f in Game.I.frogs)
            {
                if (f == null || !f.human || f.world != it.world) continue;
                Vector3 p = f.vehicle != null ? f.vehicle.transform.position : f.Center;
                float r = it.radius + (f.vehicle != null ? 1.5f : 0f);
                if ((p - it.basePos).sqrMagnitude < r * r)
                {
                    it.taken = true;
                    it.t.gameObject.SetActive(false);
                    FX.Sparkle(it.basePos, new Color(1f, 0.95f, 0.6f), 14);
                    Sfx.Play(Sfx.Pickup, 0.7f, Random.Range(0.95f, 1.15f));
                    foreach (var h in handlers) h(it, f);
                    break;
                }
            }
        }
    }
}
