using UnityEngine;

// ffu53 demo / probe hook for ONE SEA (UnderwaterWorld.cs: the underwater world is the ranch pond) + the consistency
// checks. Only with ?ffdemo=1&ffshot=onesea. The same spot by the shipwreck (pond-local (-26, 14) looking north at the wreck
// at (-30, 30)) is visited by every entry, wall-clock holds (SwiftShader is slow):
//   ph 1  P1 drives the Monster Truck into the pond there (ranch mode, sunk on the bed)
//   ph 2  P1 boards the submarine at the dock and the sub comes to the same spot (under-water mode) - the sunk truck is there too
//   ph 3  P1 swims out in scuba gear to the wreck's treasure chest
//   ph 4  P1 in the Optimus mech suit wading on the same bed
// Logs "FFDEMO onesea ..." (world, position, bed, what is near) and the C5 / C9 checks "ONESEA CHECK ...", then "FFDEMO onesea done".
[DefaultExecutionOrder(100)]
public class Ffu53 : MonoBehaviour
{
    public static Ffu53 I;
    bool started, subPlaced; int ph = -1; float phW, logT;
    Vector3 camPos, camLook; bool camOn;
    Frog f; Vehicle truck, suit;
    static Vector3 Lc(float x, float y, float z) { return Worlds.UnderO + new Vector3(x, y, z); }
    static float Bed(float x, float z) { return Worlds.UnderO.y + UnderwaterWorld.SeaY(x, z); }
    static readonly Vector2 Spot = new Vector2(-26f, 14f), Wreck = new Vector2(-30f, 30f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        if (Ffu33.Url("ffdemo") == null || Ffu33.Url("ffshot") != "onesea") return;
        I = new GameObject("ffu53").AddComponent<Ffu53>();
        DontDestroyOnLoad(I.gameObject);
    }

    void Next(int p, string what) { ph = p; phW = Time.realtimeSinceStartup; Debug.Log("FFDEMO onesea ph " + p + " " + what); }

    void Place(Vehicle v, Vector3 p, float yaw)
    {
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        v.rb.isKinematic = false; v.rb.velocity = Vector3.zero; v.rb.angularVelocity = Vector3.zero;
        v.rb.position = p; v.rb.rotation = q; v.transform.SetPositionAndRotation(p, q);
    }

    void Behind(Transform t, float back, float up, Vector3 look)
    {
        Vector3 want = t.position - t.forward * back + Vector3.up * up;
        camPos = camOn ? Vector3.Lerp(camPos, want, 1f - Mathf.Exp(-Time.deltaTime * 4f)) : want;
        camLook = look; camOn = true;
    }

    void Checks()
    {
        Ownership.LogTable();
        // C5 a: a hidden / disabled vehicle body can't be boarded (carrier stern pod)
        Vehicle tour = null;
        foreach (var v in Vehicle.All) if (v != null && v.ownerName == "Spotty" && v.Title.Contains("Tourer")) { tour = v; break; }
        if (tour != null && tour.body != null)
        {
            bool was = tour.body.enabled;
            tour.body.enabled = false;
            Vehicle n = Vehicle.Nearest(tour.transform.position, f);
            tour.body.enabled = was;
            Debug.Log("ONESEA CHECK C5 disabled Tourer body -> nearest " + (n == null ? "none" : n.Title) + (n != tour ? " PASS" : " FAIL"));
            Debug.Log("ONESEA CHECK C9 " + (Ownership.AskLine(RanchLife.I != null && RanchLife.I.robots.Count > 0 ? RanchLife.I.robots[0].robotName : "Robot", tour, f) ?? "no ask line"));
        }
        else Debug.Log("ONESEA CHECK C5 no Tourer found");
        // C5 b: eating next to the campfire -> no "put out the campfire"
        if (Campfire.I != null)
        {
            f.SendTo(WorldId.Ranch, Campfire.I.Pos + new Vector3(1.4f, 0.2f, 0f), 270f);
            Eat50.Begin(f);
            float d; Hotspot h = Interact.Nearest(Campfire.I.Pos + new Vector3(1.4f, 0f, 0f), f, out d);
            string lab = h != null ? h.Label(f) : "none";
            Debug.Log("ONESEA CHECK C5 eating by the campfire -> prompt '" + lab + "'" + (lab.Contains("put out") ? " FAIL" : " PASS"));
        }
    }

    void Update()
    {
        if (Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0 || UnderwaterWorld.I == null || UnderwaterWorld.I.sub == null) return;
        if (!started)
        {
            started = true;
            f = Game.I.frogs[Game.I.slots[0].frog];
            foreach (var v in Vehicle.All)
            {
                if (v == null) continue;
                if (truck == null && v.Title == "Monster Truck") truck = v;
                if (suit == null && v.Title.StartsWith("Optimus")) suit = v;
            }
            Debug.Log("FFDEMO onesea start: UnderO " + Worlds.UnderO + " sub at " + UnderwaterWorld.I.sub.transform.position.ToString("0.0") + " mooring " + UnderwaterWorld.Mooring.ToString("0.0") + " truck " + (truck != null) + " suit " + (suit != null));
            Checks();
            Next(0, "checks done");
        }
        float w = Time.realtimeSinceStartup - phW;
        const float Hold = 30f;
        Vector3 wreck = Lc(Wreck.x, Bed(Wreck.x, Wreck.y) - Worlds.UnderO.y + 2f, Wreck.y);
        switch (ph)
        {
            case 0:
                if (w > 3f && truck != null)
                {
                    Vector3 p = Lc(Spot.x, 0f, Spot.y); p.y = Bed(Spot.x, Spot.y) + 1.6f;
                    Place(truck, p, 0f);
                    f.SendTo(WorldId.Ranch, p + Vector3.left * 3f, 0f);
                    f.EnterVehicle(truck);
                    truck.inputHook = i => { var o = new PIn(); o.gas = 0.35f; return o; };
                    Next(1, "Monster Truck driven into the pond at the wreck (world " + f.world + ")");
                }
                break;
            case 1:
                Behind(truck.transform, 9f, 3f, wreck);
                if (w > 12f) truck.inputHook = i => new PIn();
                if (w > Hold) { truck.inputHook = null; f.ExitVehicle(); f.SendTo(WorldId.Ranch, UnderwaterWorld.Mooring + Vector3.left * 4f + Vector3.up, 90f); UnderwaterWorld.I.Dive(f); Next(2, "submarine boarded at the dock (world " + f.world + ")"); }
                break;
            case 2:
                {
                    var s = UnderwaterWorld.I.sub;
                    if (!subPlaced && s.driver == f) { subPlaced = true; Vector3 p = Lc(Spot.x, -10f, Spot.y - 4f); Place(s, p, 0f); Debug.Log("FFDEMO onesea sub moved to the wreck spot " + p.ToString("0.0")); }
                    Behind(s.transform, 11f, 2.5f, wreck);
                    if (w > Hold) { f.ExitVehicle(); f.SendTo(WorldId.Underwater, UnderwaterWorld.ChestPos + new Vector3(0f, 2.2f, -2.6f), 0f); Next(3, "scuba to the wreck's chest (world " + f.world + ")"); }
                }
                break;
            case 3:
                Behind(f.transform, 5f, 1.6f, UnderwaterWorld.ChestPos);
                if (w > Hold && suit != null)
                {
                    Vector3 p = Lc(-20f, 0f, 8f); p.y = Bed(-20f, 8f) + 2.5f;
                    Place(suit, p, 330f);
                    f.SendTo(WorldId.Ranch, p + Vector3.left * 3f, 330f);
                    f.EnterVehicle(suit);
                    Next(4, "Optimus mech suit wading on the same bed (world " + f.world + ")");
                }
                break;
            case 4:
                Behind(suit.transform, 10f, 3.5f, wreck);
                if (w > Hold) { f.ExitVehicle(); f.SendTo(WorldId.Ranch, UnderwaterWorld.Mooring + new Vector3(-6f, 1.5f, 3f), 90f); Next(6, "ranch shore view from the dock (nothing pokes out of the water)"); }
                break;
            case 6:
                camPos = UnderwaterWorld.Mooring + new Vector3(-8f, 7f, 0f); camLook = UnderwaterWorld.Mooring + new Vector3(30f, -2f, 0f); camOn = true;
                if (w > 20f) { Next(5, "done"); Debug.Log("FFDEMO onesea done chest opens " + UnderwaterWorld.ChestOpens + " last by " + UnderwaterWorld.ChestBy); }
                break;
        }
        if (Time.unscaledTime > logT && ph >= 1 && ph != 5)
        {
            logT = Time.unscaledTime + 2f;
            Vector3 p = f.vehicle != null ? f.vehicle.transform.position : f.transform.position;
            Vector3 l = p - Worlds.UnderO;
            int near = 0;
            foreach (var r in UnderwaterWorld.I.GetComponentsInChildren<Renderer>()) if ((r.bounds.center - p).sqrMagnitude < 900f) near++;
            Debug.Log("FFDEMO onesea t ph " + ph + " world " + f.world + " in " + (f.vehicle != null ? f.vehicle.Title : "-") + " local (" + l.x.ToString("0.0") + ", " + l.y.ToString("0.0") + ", " + l.z.ToString("0.0") + ") bed " + (Bed(l.x, l.z) - Worlds.UnderO.y).ToString("0.0") + " sea objects within 30 m " + near + " wreck d " + (new Vector2(l.x - Wreck.x, l.z - Wreck.y)).magnitude.ToString("0.0") + " chest opens " + UnderwaterWorld.ChestOpens);
        }
    }

    void LateUpdate()
    {
        if (!camOn || ph >= 5 || Game.I == null || Game.I.slots.Count == 0) return;
        Camera c = Game.I.slots[0].cam;
        if (c == null) return;
        c.transform.position = camPos;
        c.transform.LookAt(camLook);
    }
}
